using Microsoft.Extensions.Logging;
using NetEscapades.Extensions.Logging.RollingFile.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Joyn.Timelog.Common.Models;

namespace Joyn.Timelog.Reporting
{
    internal delegate void ViewerFiltersChangedHandler(Guid changedViewer, Dictionary<Guid, List<FilterCriteria>> currentFilters);

    internal class ViewerFiltersHandler
    {
        /// <summary>
        /// Dictionary of filters for each viewer, identified by the viewer's Guid
        /// </summary>
        private static Dictionary<Guid, List<FilterCriteria>> viewerFilterCriterias = new Dictionary<Guid, List<FilterCriteria>>();
        private static ReaderWriterLockSlim viewerFilterCriteriasLock = new ReaderWriterLockSlim();

        internal static event ViewerFiltersChangedHandler OnViewerFiltersChanged;

        private static ILogger _logger;
        private static Guid _applicationKey;

        /// <summary>
        /// Starts the ViewerFiltersHandler based on the configuration
        /// </summary>
        public static void Startup(Guid applicationKey, ILogger logger)
        {
            _applicationKey = applicationKey;
            _logger = logger;
        }

        /// <summary>
        /// Will add or update a filter to the viewer's filters
        /// </summary>
        public static void AddFilters(Guid viewerGuid, List<FilterCriteria> filters)
        {
            if(_applicationKey == Guid.Empty)
            {
                _logger?.LogWarning("Misconfiguration of ViewerFiltersHandler, ApplicationKey is empty - Check if being correcly initialized in Program.");
            }
            filters.ForEach(f => f.ReportingServerGuid = _applicationKey);

            viewerFilterCriteriasLock.EnterWriteLock();
            try
            {
                viewerFilterCriterias[viewerGuid] = filters;
            }
            finally
            {
                viewerFilterCriteriasLock.ExitWriteLock();
            }

            OnViewerFiltersChanged?.Invoke(viewerGuid, viewerFilterCriterias);
        }

        /// <summary>
        /// Will remove a filter from the viewer's filters identified by the viewer's Guid
        /// </summary>
        /// <param name="filter"></param>
        public static void RemoveFilter(FilterCriteria filter)
        {
            RemoveFilter(filter.ViewerGuid.Value);
        }

        /// <summary>
        /// Will remove a filter from the viewer's filters identified by the viewer's Guid
        /// </summary>
        /// <param name="filter"></param>
        public static void RemoveFilter(Guid viewerGuid)
        {
            viewerFilterCriteriasLock.EnterWriteLock();
            try
            {
                viewerFilterCriterias.Remove(viewerGuid);
            }
            finally
            {
                viewerFilterCriteriasLock.ExitWriteLock();
            }

            OnViewerFiltersChanged?.Invoke(viewerGuid, viewerFilterCriterias);
        }

        /// <summary>
        /// Obtains a single filter identified by it's viewers guid
        /// </summary>
        public static List<FilterCriteria> GetFilters(Guid viewerGuid)
        {
            viewerFilterCriteriasLock.EnterReadLock();
            try
            {
                if (viewerFilterCriterias.ContainsKey(viewerGuid))
                {
                    return viewerFilterCriterias[viewerGuid];
                }
                else
                {
                    return null;
                }
            }
            finally
            {
                viewerFilterCriteriasLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Obtains all filters for all viewers
        /// </summary>
        public static List<FilterCriteria> ListFilters()
        {
            viewerFilterCriteriasLock.EnterReadLock();
            try
            {
                return viewerFilterCriterias.Values.SelectMany(fc => fc.ToList()).ToList();
            }
            finally
            {
                viewerFilterCriteriasLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Produces a log package for each viewer, based on the received log messages and the viewer's filter
        /// It will then forward those packages to the viewers using the ViewerServer
        /// </summary>
        /// <param name="logMessages"></param>
        public static void SendLogMessages(List<Timelog.Common.Models.LogMessage> logMessages)
        {
            viewerFilterCriteriasLock.EnterReadLock();
            try
            {
                Dictionary<Guid, List<Timelog.Common.Models.LogMessage>> viewerPackages =
                    viewerFilterCriterias.ToDictionary(vfc => vfc.Key, vfc => logMessages.FindAll(lm => vfc.Value.Any(f => f.Matches(lm))));

                foreach (var viewerPackage in viewerPackages)
                {
                    if (viewerPackage.Value.Any())
                    {
                        ViewerServer.SendLogMessages(viewerPackage.Key, viewerPackage.Value);
                    }
                }
            }
            finally
            {
                viewerFilterCriteriasLock.ExitReadLock();
            }
        }
    }
}
