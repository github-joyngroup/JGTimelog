using OfficeOpenXml;
using OfficeOpenXml.Style;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelog.Common.Models;

namespace Timelog.Viewer.Tester
{
    internal class Excel
    {

        public static bool Produce(string excelFilePath, string logFilePath, int numberColumns)
        {
            List<LogMessage> logMessages = new List<LogMessage>();

            string[] lines = File.ReadAllLines(logFilePath);
            

            foreach (string line in lines)
            {
                string[] fields = line.Split('|').Select(field => field.Trim()).ToArray();
                try
                {
                    LogMessage logMessage = new LogMessage
                    {
                        //ApplicationKey = Guid.Parse(fields[0]),
                        Domain = IPAddressToUInt(fields[1]), // Remove any dots from the IP address
                        //ClientLogLevel = int.Parse(fields[2]),
                        //ClientTag = long.Parse(fields[3]),
                        TransactionID = Guid.Parse(fields[4]),
                        Command = (Commands)Enum.Parse(typeof(Commands), fields[5]),
                        //OriginTimestamp = DateTime.Parse(fields[6]),
                        TimeServerTimeStamp = DateTime.Parse(fields[7]),
                        //ExecutionTime = string.IsNullOrEmpty(fields[8]) ? (TimeSpan?)null : TimeSpan.Parse(fields[8]),
                        //Reserved = string.IsNullOrEmpty(fields[9]) ? new byte[0] : Encoding.UTF8.GetBytes(fields[9]),
                        //MessageHeader = string.IsNullOrEmpty(fields[10]) ? new byte[0] : Encoding.UTF8.GetBytes(fields[10]),
                        //MessageData = string.IsNullOrEmpty(fields[11]) ? new byte[0] : Encoding.UTF8.GetBytes(fields[11])
                    };
                    logMessages.Add(logMessage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing log message: {ex.Message}");
                }
                
            }

            //Get the lowest and highest TimeServerTimeStamps
            DateTime ? lowestTimeStamp = logMessages.Min(log => log.TimeServerTimeStamp);
            DateTime? highestTimeStamp = logMessages.Max(log => log.TimeServerTimeStamp);

            if(!lowestTimeStamp.HasValue || !highestTimeStamp.HasValue)
            {
                return false;
            }

            // Calculate the total interval in seconds
            double totalIntervalSeconds = (highestTimeStamp.Value - lowestTimeStamp.Value).TotalSeconds;

            Dictionary<Guid, Dictionary<int, List<string>>> organizedLogs = new Dictionary<Guid, Dictionary<int, List<string>>>();

            // Check if the total interval in seconds is non-zero
            if (totalIntervalSeconds != 0)
            {
                // Create the organizedLogs dictionary using the grouping logic
                organizedLogs = logMessages
                    .Where(log => log.TimeServerTimeStamp.HasValue)
                    .GroupBy(log => log.TransactionID)
                    .ToDictionary(
                        group => group.Key,
                        group => group.GroupBy(log => Convert.ToInt32((log.TimeServerTimeStamp.Value - lowestTimeStamp.Value).TotalSeconds / totalIntervalSeconds * (numberColumns - 1))) //-1 to avoid out of bounds
                            .ToDictionary(subGroup => subGroup.Key, subGroup => subGroup.Select(log => $"{log.TimeServerTimeStamp.Value.ToString("HH:mm:ss")} - {HelperViewer.UIntToIPAddress(log.Domain)} - {SetCommand(log.Command)}").ToList())
                    );
            }
            else
            {
                return false;
            }


            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // Create a new Excel file and write the organized data into it
            using (ExcelPackage excelPackage = new ExcelPackage())
            {
                ExcelWorksheet worksheet = excelPackage.Workbook.Worksheets.Add("Logs");

                // Write the organized data into the Excel worksheet
                int startRowLog = 2;

                // Write the headers for the columns
                worksheet.Cells[startRowLog, 2].Value = "Transaction ID";

                // Write the headers for the columns
                for (int i = 0; i < numberColumns; i++)
                {
                    worksheet.Cells[startRowLog, i + 3].Value = $"{lowestTimeStamp.Value.AddSeconds(totalIntervalSeconds / numberColumns * i).ToString("HH:mm:ss")}";
                }

                // Increment the row for the next entry
                var row = startRowLog++;

                // Know the last row to write the logs
                var lastRow = startRowLog;

                // Iterate through the organized logs to populate the table
                foreach (var transactionLogs in organizedLogs)
                {
                    // Write the transaction ID to the first column
                    worksheet.Cells[startRowLog, 2].Value = transactionLogs.Key.ToString();

                    foreach (var timeLogs in transactionLogs.Value)
                    {
                        int timestamp = timeLogs.Key;

                        foreach (string log in timeLogs.Value)
                        {
                            //Find the column to write the log
                            int column = timestamp + 3;

                            row = startRowLog;

                            //Verify for that column starting by the startRow, which one is empty
                            while (worksheet.Cells[row, column].Value != null)
                            {
                                row++;
                            }

                            if(row > lastRow)
                            {
                                lastRow = row + 1;
                            }

                            // Write the log into the cell
                            worksheet.Cells[row, column].Value = log;
                        }
                    }

                    // Increment the row for the next entry
                    startRowLog = lastRow + 1;
                }

                //Adjust all excel
                AdjustExcel(worksheet, lastRow, numberColumns);

                //Save the file
                excelPackage.SaveAs(new FileInfo(excelFilePath));
            }
            //Excel creation is successful
            return true;
        }

        public static string SetCommand(Commands command)
        {
            switch (command)
            {
                case Commands.Start:
                    return "B";
                case Commands.Stop:
                    return "E";
                case Commands.Normal:
                    return "N";
                default:
                    return "N";
            }
        }

        //IP Address to UInt
        public static uint IPAddressToUInt(string ipAddress)
        {
            byte[] bytes = ipAddress.Split('.')
                .Select(byte.Parse)
                .ToArray();
            if (BitConverter.IsLittleEndian)
            {
                // Reverse the byte array if the system architecture is little endian
                Array.Reverse(bytes);
            }
            return BitConverter.ToUInt32(bytes, 0);
        }

        //Adjust the excel
        private static void AdjustExcel(ExcelWorksheet worksheet, int lastRow, int numberColumns)
        {
            //Set the column width
            worksheet.Column(2).Width = 37;
            for (int i = 0; i < numberColumns; i++)
            {
                worksheet.Column(i + 3).Width = 22;
            }

            //Set the row height
            for (int i = 1; i <= lastRow; i++)
            {
                worksheet.Row(i).Height = 22;
            }

            //Set the font
            worksheet.Cells[2, 2, lastRow, numberColumns + 2].Style.Font.Name = "Arial";
            worksheet.Cells[2, 2, lastRow, numberColumns + 2].Style.Font.Size = 10;

            //Set the alignment
            worksheet.Cells[2, 2, lastRow, numberColumns + 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells[2, 2, lastRow, numberColumns + 2].Style.VerticalAlignment = ExcelVerticalAlignment.Center;


        }
    }

}