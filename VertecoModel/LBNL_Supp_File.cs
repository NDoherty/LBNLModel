using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Verteco.Shared;

namespace VertecoModel
{
    class LBNL_Supp_File : LBNL_Base_File
    {
        #region const Field names from LBNL
        private const int DEFAULT_MIN_WINDOW_SIZE = 5;
        private const int DEFAULT_MAX_WINDOW_SIZE = 15;
        private const int DEFAULT_ENERGY_THRESHOLD = 2;

        // Header Section
        private const string H001_BuildingId = "BUILDINGID";
        private const string H002_LagWindow = "LAGWINDOW";
        private const string H003_WorkingDays = "WORKINGDAYS";
        private const string H004_TrainingEnergytoModel = "ENERGYTOMODEL";
        private const string H005_TrainingSeasonStartDate = "TRAININGSEASONSTARTDATE";
        private const string H006_TrainingSeasoneEndDate = "TRAININGSEASONENDDATE";
        private const string H010_WorkingDayEnd = "WORKINGDAYEND";
        private const string H011_MinWindowSize = "MINWINDOWSIZE";
        private const string H012_MaxWindowSize = "MAXWINDOWSIZE";
        private const string H013_EnergyThreshold = "ENERGYTHRESHOLD";

        // Threshold
        private const string T001_NonWorkingDays = "NONWORKINGDAYS";

        // Mandatory header fields
        private string[] RequiredHeaderFields = {   H001_BuildingId, 
                                                    H002_LagWindow, 
                                                    H003_WorkingDays,           
                                                    H004_TrainingEnergytoModel,
                                                    H005_TrainingSeasonStartDate,
                                                    H006_TrainingSeasoneEndDate
                                                };
        #endregion
        #region member variables
        private double _lagWindow;
        private double _energyThreshold = DEFAULT_ENERGY_THRESHOLD;

        private int _minWindowSize = DEFAULT_MIN_WINDOW_SIZE;
        private int _maxWindowSize = DEFAULT_MAX_WINDOW_SIZE;

        private List<DayOfWeek> _workingDays;
        private List<DateTime> _nonWorkingDays;
        private DateTime _workingDayEndTime;

        #endregion
        #region properties
        /// <summary>
        /// The Energy To Model is the particular energy measurement we are hoping to predict
        /// </summary>
        public string EnergyToModel
        {
            get { return _header[H004_TrainingEnergytoModel]; }
        }
        /// <summary>
        /// Working Days 
        /// </summary>
        public List<DayOfWeek> WorkingDays
        {
            get { return _workingDays; }
        }
        public List<DateTime > NonWorkingDays
        {
            get { return _nonWorkingDays; }
        }

        public string TrainingSeasonStartDateMMDD
        {
            get { return _header[H005_TrainingSeasonStartDate]; }
        }
        public string TrainingSeasonEndDateMMDD
        {
            get { return _header[H006_TrainingSeasoneEndDate]; }
        }

        public double LagWindow { get { return _lagWindow; } }
        public double EnergyThreshold { get { return _energyThreshold; } }
        public int MinWindowSize { get { return _minWindowSize; } }
        public int MaxWindowSize { get { return _maxWindowSize; } }
        public DateTime WorkingDayEndTime { get { return _workingDayEndTime; } }
        
        #endregion
        #region construction/destruction
        public LBNL_Supp_File(string filename) : this () 
        {
            _filename = filename;
        }

        public LBNL_Supp_File()
        {
            _workingDayEndTime  = DateTime.Parse("18:00:00");
            _workingDays        = new List<DayOfWeek>();
            _nonWorkingDays     = new List<DateTime>();
        }
        #endregion
        #region public methods
        public bool CheckFile()
        {
            bool bSuccess = true;

            foreach (string reqField in RequiredHeaderFields)
            {
                if (!_header.ContainsKey(reqField))
                {
                    // Missing mandatory field
                    string message = "Supplementary file [" + _filename + "] - Mandatory field missing [" + reqField + "].";
                    Logger.LogMessage(MessageType.Error, message);
                    bSuccess = false;

                }
            }

            // Now more more specific checks
            // 1. WorkingDays has max length 7, only digits 1-7 are allowed, duplicates are allowed but are eliminated
            bSuccess = bSuccess && CheckLagAndOtherParameters();
            bSuccess = bSuccess && CheckWorkingDaysField();
            bSuccess = bSuccess && CheckCoolingParameters();
            bSuccess = bSuccess && CheckHeatingParameters();
            bSuccess = bSuccess && CheckHeatingOrCoolingPresent();

            return bSuccess;
        }
        public bool ParseNonWorkingDays()
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
                        if (s.Length >= T001_NonWorkingDays.Length && s.Substring(0, T001_NonWorkingDays.Length).ToUpper().Equals(T001_NonWorkingDays))
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
                                    this.NonWorkingDays.Add(currentHoliday);
                                }
                                else
                                {
                                    // shouldnt happen - date should be well formatted
                                    Logger.LogMessage(MessageType.Warning, "Invalid Date format:[" + tsDataLine  + "], skipping...");
                                }
                            }
                        }
                    }
                }
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
        private bool CheckWorkingDaysField()
        {
            bool bSuccess = true;

            _workingDays = new List<DayOfWeek>(_header[H003_WorkingDays].Length);

            // 1. all numeric & in range 1..7, 1=Sunday (SQL style)
            foreach (char ch in _header[H003_WorkingDays].ToCharArray())
            {
                if (ch >= '1' && ch <= '7')
                {
                    DayOfWeek day;
                    day = (DayOfWeek)(int)(ch - '1'); ;
                    
                    // Add to List, no duplicates added though
                    if (!_workingDays.Contains(day))
                    {
                        _workingDays.Add(day);
                    }
                }
                else
                {
                    // not in range
                    Logger.LogMessage(MessageType.Error, "Weekday not in range 1..7, quiting...");
                    bSuccess = false;
                }
            }
            return bSuccess;
        }
        private bool CheckLagAndOtherParameters()
        {
            bool bSuccess = true;

            // Lag window must be present, the rest are optional
            if (0 ==_header[H002_LagWindow].Length)
            {
                return false;
            }
  
            bSuccess = double.TryParse(_header[H002_LagWindow], out _lagWindow);


            ExtractParameter(H011_MinWindowSize, ref  _minWindowSize);
            ExtractParameter(H012_MaxWindowSize, ref  _maxWindowSize);
            ExtractParameter(H010_WorkingDayEnd, ref  _workingDayEndTime);
            ExtractParameter(H013_EnergyThreshold, ref  _energyThreshold);

            // WorkingDayEnd - Lag - Max Window size must not go into previous day
            if (_workingDayEndTime.AddHours(-_lagWindow).Hour < _maxWindowSize)
            {

                Logger.LogMessage(MessageType.Warning, "Max window size has been reduced to [" + _workingDayEndTime.AddHours(-_lagWindow).Hour + "] from [" + _maxWindowSize.ToString() + "]");  
                _maxWindowSize = _workingDayEndTime.AddHours(-_lagWindow).Hour;

            }
              
            
            return bSuccess;
        }

        /// <summary>
        /// TODO TEST ALL CASES
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
                    Logger.LogMessage(MessageType.Information, "Extracted ["+ fieldName + "] = ["+_header[fieldName]+"] from Supp File: [" + result.ToString() + "]");
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

        private bool CheckCoolingParameters()
        {
            bool bSuccess = true;

            // All 3 params must be present or absent
            return bSuccess;
        }
        private bool CheckHeatingParameters()
        {
            bool bSuccess = true;

            // All 3 params must be present or absent
            return bSuccess;
        }
        private bool CheckHeatingOrCoolingPresent()
        {
            bool bSuccess = true;

            // either heating set or cooling set must be present
            return bSuccess;
        }
        #endregion


    }
}
