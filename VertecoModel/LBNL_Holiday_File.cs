using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Verteco.Shared;

namespace VertecoModel
{
    class LBNL_Holiday_File : LBNL_Base_File
    {
        #region const Field names from LBNL

        // Header Section - there is no header section in a holiday file

        // Holidays
        private const string T001_Date = "DATE";

       
        #endregion
        #region member variables

        private List<DateTime> _nonWorkingDays;

        #endregion
        #region properties
        public List<DateTime > Holidays
        {
            get { return _nonWorkingDays; }
        }

        #endregion
        #region construction/destruction
        public LBNL_Holiday_File(string filename) : this () 
        {
            _filename = filename;
        }

        public LBNL_Holiday_File()
        {
             _nonWorkingDays     = new List<DateTime>();
        }
        #endregion
        #region public methods
        public bool CheckFile()
        {
            bool bSuccess = true;

            // nothing to check
            return bSuccess;
        }
        /// <summary>
        /// Parse the holidays/non-working days from the file
        /// These are in the format YYYY-MM-DD
        /// </summary>
        /// <returns></returns>
        public bool ParseHolidays()
        {
            bool bSuccess = true;

            if (File.Exists(_filename))
            {
                // Open the file to read from. 
                using (StreamReader sr = File.OpenText(_filename))
                {
                    // Skip the comments until we get to the header
                    string s = "";
                    while ((s = sr.ReadLine()) != null)
                    {
                        // Skipahead until we get to the timeseries header
                        // Check to see if its the beginning of our time series data
                        if (s.Length >= T001_Date.Length && s.Substring(0, T001_Date.Length).ToUpper().Equals(T001_Date))
                        {
                            // this is the header line for the holiday/nonworking days data
                            // as there's only one column we juat read it line by line

                            string tsDataLine = "";
                            while ((tsDataLine = sr.ReadLine()) != null)
                            {

                                DateTime currentHoliday;

                                // Datefield should always be valid 
                                if (DateTime.TryParse(tsDataLine, out currentHoliday))
                                {
                                    this.Holidays.Add(currentHoliday);
                                }
                                else
                                {
                                    // shouldnt happen - date should be well formatted
                                    Logger.LogMessage(MessageType.Warning, "Holiday file - invalid Date format:[" + tsDataLine  + "], skipping...");
                                }
                            }
                        }
                    }
                }
                // Log our success
                string message = "Holiday File [" + this._filename + "] successfully read.";
                Logger.LogMessage(MessageType.Warning, message);

            }
            else
            {
                // File does not exist
                string message;
                message = "File does not exist [" + _filename + "]";
                Logger.LogMessage(MessageType.Error, message);

            }


            return bSuccess;
        }

        #endregion
        #region Helper functions

        /// <summary>
        /// TODO TEST ALL CASES - this belongs in the base class
        /// </summary>
        /// <param name="fieldName"></param>
        /// <param name="result"></param>
        private void ExtractParameter(string fieldName, ref double result)
        {
            // If field exists, and it has data, we try and parse it
            if (_header.ContainsKey(fieldName) && (_header[fieldName].Length > 0))
            {
                if (double.TryParse(_header[fieldName], out result))
                {
                    Logger.LogMessage(MessageType.Information, "Extracted [" + fieldName + "] = [" + _header[fieldName] + "] from Supp File: [" + result.ToString() + "]");
                }
                else
                {
                    // Failed to Parse 
                    Logger.LogMessage(MessageType.Warning, "[" + fieldName + "] format error in Supp File, using default value: [" + result + "]");
                }
            }
            else
            {
                // no data
                Logger.LogMessage(MessageType.Warning, "[" + fieldName + "] absent or empty, using default value: [" + result + "]");
            }
        }

        private void ExtractParameter(string fieldName, ref int result)
        {
            // If field exists, and it has data, we try and parse it
            if (_header.ContainsKey(fieldName) && (_header[fieldName].Length > 0))
            {
                if (int.TryParse(_header[fieldName], out result))
                {
                    Logger.LogMessage(MessageType.Information, "Extracted [" + fieldName + "] = [" + _header[fieldName] + "] from Supp File: [" + result.ToString() + "]");
                }
                else
                {
                    // Failed to Parse 
                    Logger.LogMessage(MessageType.Warning, "[" + fieldName + "] format error in Supp File, using default value: [" + result + "]");
                }
            }
            else
            {
                // no data
                Logger.LogMessage(MessageType.Warning, "[" + fieldName + "] absent or empty, using default value: [" + result + "]");
            }
        }

        private void ExtractParameter(string fieldName, ref DateTime result)
        {
            // If field exists, and it has data, we try and parse it
            if (_header.ContainsKey(fieldName) && (_header[fieldName].Length > 0))
            {
                if (DateTime.TryParse(_header[fieldName], out result))
                {
                    Logger.LogMessage(MessageType.Information, "Extracted ["+ fieldName+ "] = ["+_header[fieldName]+"] from Supp File: [" + result.ToString() + "]");
                }
                else
                {
                    // Failed to Parse 
                    Logger.LogMessage(MessageType.Warning, "["+fieldName+"] format error in Supp File, using default value: [" + result.ToString() + "]");
                }
            }
            else
            {
                // no data
                Logger.LogMessage(MessageType.Warning, "[" + fieldName + "] absent or empty, using default value: [" + result.ToString() + "]");

            }
          
        }

        #endregion


    }
}
