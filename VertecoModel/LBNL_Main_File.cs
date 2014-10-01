using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Verteco.Shared;

namespace VertecoModel
{
    class LBNL_Main_File :LBNL_Base_File 
    {
        #region const Field names from LBNL 

        // Header Section
        private const string H001_BuildingId                = "BUILDINGID";
        private const string H002_zip                       = "ZIP";
        private const string H003_floorareaSF               = "FLOORAREA.SF";
        private const string H004_buildingtypeNAICS         = "BUILDINGTYPE.NAICS";
        private const string H005_buildingtypeSTR           = "BUILDINGTYPE.STR";

        // Timeseries
        private const string T001_timestampUTC              = "TIME.LOCAL";          
        private const string T002_wbelectricitykWh          = "WBELECTRICITY.KWH";      
        private const string T003_wbgaskBTU                 = "WBGAS.KBTU";             
        private const string T004_chwkBTU                   = "CHW.KBTU";               
        private const string T005_hwkBTU                    = "HW.KBTU";                
        private const string T006_steamkBTU                 = "STEAM.KBTU";             
        private const string T007_coolingelectricitykWh     = "COOLINGELECTRICITY.KWH"; 
        private const string T008_coolinggaskBTU            = "COOLINGGAS.KBTU";        
        private const string T009_heatingelectricitykWh     = "HEATINGELECTRICITY.KWH"; 
        private const string T010_heatingGaskBTU            = "HEATINGGAS.KBTU";          
        private const string T011_ventiliationElectricitykWh= "VENTILIATIONELECTRICITY.KWH";      
        private const string T012_lightingElectricitykWh    = "LIGHTINGELECTRICITY.KWH";             
        private const string T013_dbOATF                      = "DBOAT.F";               
        private const string T014_wbOATF                      = "WBOAT.F";                
        private const string T015_rhPercent                 = "RH.PERCENT";             
        private const string T016_ahGM3                     = "AH.GM3"; 
        private const string T017_dptempF                   = "DTEMP.F";        
        private const string T018_tmydbtF                   = "TMYDBT.F"; 
        private const string T019_tmydbtF                   = "TMYDBT.F"; 
        private const string T020_windspeedMPH              = "WINDSPEED.MPH";
        private const string T021_schedule                  = "SCHEDULE";
        private const string T022_occupancyPercent          = "OCCUPANCY.PERCENT";
        private const string T023_occupancyPersons          = "OCCUPANCY.PERSONS";
        private const string T024_occupancySF               = "OCCUPANCY.SF";
        private const string T025_occupancyPPM              = "OCCUPANCY.PPM";
        private const string T026_waterGal                  = "WATER.GAL";

#endregion

        #region private member variables
        // Mandatory header fields
        private  string[] RequiredHeaderFields = { H001_BuildingId };
        private string[] RequiredDataFields = { T001_timestampUTC };
        private string[] TemperatureFields = {  
                                            T013_dbOATF           
                                            ,T014_wbOATF                 
                                            };
        private string[] EnergyFields = {  
                                            T002_wbelectricitykWh          
                                            ,T003_wbgaskBTU                 
                                            ,T004_chwkBTU                   
                                            ,T005_hwkBTU                    
                                            ,T006_steamkBTU                 
                                            ,T007_coolingelectricitykWh     
                                            ,T008_coolinggaskBTU            
                                            ,T009_heatingelectricitykWh     
                                            ,T010_heatingGaskBTU            
                                            ,T011_ventiliationElectricitykWh
                                            ,T012_lightingElectricitykWh   
                                            
                                            };
        
        private System.Globalization.CultureInfo usa = new System.Globalization.CultureInfo("en-us");

        // The Data to analyse - This is important!!!
        /// <summary>
        /// This is the complete (Interval) timeseries of energy measures used for training the model
        /// </summary>
        private Dictionary<DateTime, double> _energyTS = new Dictionary<DateTime, double>();
        /// <summary>
        /// This is the complete (Interval) timeseries of temperatures used for training the model
        /// </summary>
        Dictionary<DateTime, double> _temperatureTS = new Dictionary<DateTime, double>();
        /// <summary>
        /// The name of the energy field to analyse (stored in UPPERCASE)
        /// </summary>
        private string _EnergyFieldName;
        /// <summary>
        /// The name of the Temperature field Typically "dboat.F" (stored in UPPERCASE)
        /// </summary>
        private string _TemperatureFieldName;
        #endregion
        #region properties
        public string BuildingId { get { /* BuildingId is mandatory, therefore will exist */ return Header[H001_BuildingId]; } }
        public string BuildingTypeId { 
                                        get 
                                        {
                                            if (Header.ContainsKey(H004_buildingtypeNAICS))
                                            {
                                                return Header[H004_buildingtypeNAICS];
                                            }
                                            else
                                            {
                                                return "";
                                            }
                                        }
                                     }
        /// <summary>
        /// The name of the energy field to analyse
        /// </summary>
        public string EnergyFieldName { get { return _EnergyFieldName; }   set { _EnergyFieldName = value.ToUpper(); } }
        /// <summary>
        /// The name of the temperature field to analyse Typically "dboat.F" 
        /// </summary>
        public string TemperatureFieldName { get { return _TemperatureFieldName; } set { _TemperatureFieldName = value.ToUpper(); } }

        /// <summary>
        /// This is the complete (Interval) timeseries of energy measures used for training the model 
        /// </summary>
        public Dictionary<DateTime, double> EnergyTS { get { return _energyTS; } }
        /// <summary>
        /// This is the complete (Interval) timeseries of temperatures used for training the model
        /// </summary>
        public Dictionary<DateTime, double> OATempTS { get { return _temperatureTS; } }
        /// <summary>
        /// Setting the start date limits the analysis to those days between start & end date
        /// </summary>
        public int StartDateMMDD { get; set; }
        /// <summary>
        /// Setting the end date limits the analysis to those days between start & end date
        /// </summary>
        public int EndDateMMDD { get; set; }

        #endregion
        #region construction/destruction
        public LBNL_Main_File(string filename) : this()
        {
            _filename = filename;
            
        }
        /// <summary>
        /// Constructor, sets the temperature fieldname to dbt.F by default
        /// </summary>
        public LBNL_Main_File()
        {
            TemperatureFieldName = T013_dbOATF;

            // default to all year as training period
            StartDateMMDD = 0101;
            EndDateMMDD = 1231;
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
                    string message = "Main file [" + _filename + "] - Mandatory field missing [" + reqField + "].";
                    Logger.LogMessage(MessageType.Error, message);
                    bSuccess = false;

                }
            }

            // Now more more specific checks
            // 1. WorkingDays has max length 7, only digits 1-7 are allowed, duplicates are allowed but are eliminated
            
            return bSuccess;
        }
        /// <summary>
        /// Read the intervaldata from the file and store it in the two Dictionary collections _energyTS and _temperatureTS
        /// </summary>
        /// <param name="includedDays">Weekdays to include (Sun = 1)</param>
        /// <returns>false if parsing fails completely</returns>
        public bool ParseTimeSeries()
        {
            bool bSuccess = true;
            Logger.LogMessage(MessageType.Information, "Parsing interval data START");
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
                        if (s.Length > T001_timestampUTC.Length && s.Substring(0, T001_timestampUTC.Length).ToUpper().Equals(T001_timestampUTC))
                        {
                            // this is the header line for the timeseries data
                            // Header
                            string[] TSheaders = s.Split(SEPARATOR);

                            // Get the temperature and energyfield to analyse
                            TemperatureFieldName = GetTemperatureField(TSheaders);
                            EnergyFieldName = GetEnergyField(TSheaders);
                            bSuccess = TemperatureFieldName.Length > 0 && EnergyFieldName.Length > 0 ;
                            if (bSuccess)
                            {
                                string tsDataLine = "";
                                int datapointCount = 0;
                                while ((tsDataLine = sr.ReadLine()) != null)
                                {
                                    // parse the comma delimited data into separate fields
                                    string[] tsdata = tsDataLine.Split(SEPARATOR);
                                    Dictionary<string, string> tsDataDictionary = CreateDictionaryFromPairs(TSheaders, tsdata);

                                    DateTime currentDate;

                                    // Datefield should always be valid 
                                    // V1.3 Date time in the main file is now in quasi-US format (MM/DD/YY HH:MM) HH:MM is 24 hour format, year may be 2 or 4 digit
                                    // e.g. 12/25/14 15:45 is time for Christmas dinner
                                    // Note that the holiday file is still in the ISO format yyyy-mm-dd
                                    if (DateTime.TryParse(tsDataDictionary[T001_timestampUTC], usa.DateTimeFormat, System.Globalization.DateTimeStyles.None, out currentDate))
                                    {
                                        int currentDateMMDD;
                                        currentDateMMDD = currentDate.Month * 100 + currentDate.Day;

                                        // only care about data in range This is complicated by the fact that for the winter training period the start date is usually > end Date
                                        // - remember this is in the format MMDD as data from multiple years can be used to train the model

                                        if (DateinRange(currentDateMMDD))
                                        {
                                            // Valid date so hopefully there's energy data & temperature data
                                            double energy = double.NaN;
                                            double temperature = double.NaN;

                                            if (!double.TryParse(tsDataDictionary[EnergyFieldName], out energy))
                                            {
                                                Logger.LogMessage(MessageType.Information, "failed to read energy data for [" + currentDate + "]");
                                            }
                                            if (!double.TryParse(tsDataDictionary[TemperatureFieldName], out temperature))
                                            {
                                                Logger.LogMessage(MessageType.Information, "failed to read temperature data for [" + currentDate + "]");
                                            }

                                            ////////////////////////////////////////////////////////////////////////////////
                                            // Add an entry to both arrays (keep them synched) (even if we've no readings?)
                                            ////////////////////////////////////////////////////////////////////////////////
                                           
                                            _temperatureTS.Add(currentDate, temperature);
                                            _energyTS.Add(currentDate, energy);
                                            datapointCount++;
                                          
                                        }
                                    }
                                    else
                                    {
                                        // shouldnt happen - date should be well formatted
                                        Logger.LogMessage(MessageType.Warning, "Invalid Date format:[" + tsDataDictionary[T001_timestampUTC] + "], skipping...");
                                    }
                                }
                            } //if
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

            Logger.LogMessage(MessageType.Information, "Parsing interval data END");
            Logger.LogMessage(MessageType.Information, "[" +_temperatureTS.Count.ToString() +"] records successfully parsed");
            
            return bSuccess;
        }

#endregion
        #region Helper functions
        private string GetTemperatureField(string[] fieldnames)
        {
            string tempField = "";

            foreach (string fieldname in fieldnames)
            {
                if (TemperatureFields.Contains(fieldname.ToUpper()))
                {
                    // Got the first one!
                    tempField = fieldname;
                    break;
                }
            }
            return tempField;
        }
        private string GetEnergyField(string[] fieldnames)
        {
            string energyField = "";

            foreach(string fieldname in fieldnames)
            {
                if (EnergyFields.Contains(fieldname.ToUpper()))
                {
                    // Got the first one!
                    energyField = fieldname;
                    break;
                }
            }
            return energyField;
        }

        private bool DateinRange(int currentDateMMDD)
        {
            if (StartDateMMDD < EndDateMMDD)
            {
                return currentDateMMDD >= StartDateMMDD && currentDateMMDD <= EndDateMMDD;
            }
            else
            {
                return currentDateMMDD > StartDateMMDD || currentDateMMDD < EndDateMMDD;
            }
        }
        #endregion
    }



}
