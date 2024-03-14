using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelog.Common.Models;

namespace Timelog.Reporting
{
    internal class ViewerFiltersHandler
    {
        /// <summary>
        /// Dictionary of filters for each viewer, identified by the viewer's Guid
        /// </summary>
        private static Dictionary<Guid, FilterCriteria> viewerFilterCriterias = new Dictionary<Guid, FilterCriteria>();
        private static ReaderWriterLockSlim viewerFilterCriteriasLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Will add or update a filter to the viewer's filters
        /// </summary>
        public static void AddFilter(FilterCriteria filter)
        {
            filter.ReportingServerGuid = ReportingServer.Configuration.AppKey;
            viewerFilterCriteriasLock.EnterWriteLock();
            try
            {
                viewerFilterCriterias[filter.ViewerGuid.Value] = filter;
            }
            finally
            {
                viewerFilterCriteriasLock.ExitWriteLock();
            }
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
        }

        /// <summary>
        /// Obtains a single filter identified by it's viewers guid
        /// </summary>
        public static FilterCriteria GetFilter(Guid viewerGuid)
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
                return viewerFilterCriterias.Values.ToList();
            }
            finally
            {
                viewerFilterCriteriasLock.ExitReadLock();
            }
        }
    }
}
