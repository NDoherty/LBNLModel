using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Verteco.Shared;

namespace VertecoModel
{
    class LBNL_Forecast_File :LBNL_Base_File 
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
        private const string T013_dboatF                    = "DBOAT.F";               
        private const string T014_wboatF                    = "WBOAT.F";                
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

        #region member variables
        // Mandatory header fields
        private  string[] RequiredHeaderFields = { H001_BuildingId };
        private string[] RequiredDataFields = { T001_timestampUTC };


        // The Data to analyse - This is important!!!
        /// <summary>
        /// 
        /// </summary>
        Dictionary<DateTime, double> _temperatureTS = new Dictionary<DateTime, double>();
        
        #endregion
        #region properties
        // Parameters for data gathering
        private string _EnergyFieldName;
        private string _TemperatureFieldName;

        public string EnergyFieldName { get { return _EnergyFieldName; }   set { _EnergyFieldName = value.ToUpper(); } }
        public string TemperatureFieldName { get { return _TemperatureFieldName; } set { _TemperatureFieldName = value.ToUpper(); } }
        public Model ForecastModel { get; set; }
        public Dictionary<DateTime, double> EnergyTS { get; set; }
        public Dictionary<DateTime, double> OATempTS { get { return _temperatureTS; } }
        public int StartDateMMDD { get; set; }
        public int EndDateMMDD { get; set; }

        #endregion
        #region construction/destruction
        public LBNL_Forecast_File(string filename, string energyFieldname) 
        {
            _filename = filename;
            _EnergyFieldName = energyFieldname;
            TemperatureFieldName = T013_dboatF;
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
                    string message = "Forecast file [" + _filename + "] - Mandatory field missing [" + reqField + "].";
                    Logger.LogMessage(MessageType.Error, message);
                    bSuccess = false;

                }
            }

            // Now more more specific checks
            // 1. WorkingDays has max length 7, only digits 1-7 are allowed, duplicates are allowed but are eliminated
            
            return bSuccess;
        }

        /// <summary>
        /// Time series should contain interval data at an hourly/quarter hourly frequency
        /// We expect the file to contain the timestamp & temperatures for the forecast period
        /// Hourly (or total energy) will be output on a daily basis (at YYYY-MM-DD 00:00)
        /// </summary>
        /// <param name="includedDays"></param>
        /// <returns></returns>
        public bool ParseTimeSeries()
        {
            bool bSuccess = false;

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
                            // found the time series data
                            bSuccess = true;

                            // this is the header line for the timeseries data
                            // Header
                            string[] TSheaders = s.Split(SEPARATOR);
                            string tsDataLine = "";
                            int datapointCount = 0;
                            while ((tsDataLine = sr.ReadLine()) != null)
                            {
                                // parse the comma delimited data into separate fields
                                string[] tsdata = tsDataLine.Split(SEPARATOR);
                                Dictionary<string, string> tsDataDictionary = CreateDictionaryFromPairs(TSheaders, tsdata);

                                DateTime currentDate;

                                // Datefield should always be valid 
                                if (DateTime.TryParse(tsDataDictionary[T001_timestampUTC], out currentDate))
                                {

                                    // only care about data in range - remeber this is in the format MMDD as data from multiple years can be used to train the model
                                    if (true)
                                    {

                                        double temperature = double.NaN;

                                        if (!double.TryParse(tsDataDictionary[TemperatureFieldName], out temperature))
                                        {
                                            Logger.LogMessage(MessageType.Information, "failed to read temperature data for [" + currentDate + "]");
                                            temperature = double.NaN;
                                        }

                                        ////////////////////////////////////////////////////////////////////////////////
                                        // Add an entry to temperature array (keep them synched) (even if we've no readings?)
                                        ////////////////////////////////////////////////////////////////////////////////

                                        _temperatureTS.Add(currentDate, temperature);
                                        datapointCount++;

                                    }
                                }
                                else
                                {
                                    // shouldnt happen - date should be well formatted
                                    Logger.LogMessage(MessageType.Warning, "Invalid Date format:[" + tsDataDictionary[T001_timestampUTC] + "], skipping...");
                                }
                            }
                        }
                    }
                }

                // Check to see if we found any time series data...
                if (!bSuccess)
                {
                    // Should not happen - no local.time header
                    Logger.LogMessage(MessageType.Error, "No timeseries ["+ T001_timestampUTC  +"] detected in Forecast file.  Forecast cannot be made");
                }
            }
            else
            {
                // File does not exist
                string message;
                message = "File does not exist [" + _filename + "]";
                Logger.LogMessage(MessageType.Error, message);
                bSuccess = false;
            }

            
            return bSuccess;
        }
        public bool WriteToFile()
        {
            bool bSuccess = true;
            string _filename = this.Filename.Substring(0, this.Filename.LastIndexOf('\\')+1) + DateTime.Now.ToString("yyyy.MM.dd.")+ this.Filename.Substring(this.Filename.LastIndexOf('\\')+1); ;

            if (File.Exists(_filename))
            {
                // we're going to overwrite it
                Logger.LogMessage(MessageType.Warning, "Output file [" + _filename + "] already exists, overwriting...");
            }
            try
            {
                StreamWriter sw = File.CreateText(_filename);

                // First a few comments
                sw.WriteLine("# Forecast generated by Verteco Lag Model " + DateTime.Now.ToString());
                sw.WriteLine("# Model type: [{0}]", this.ForecastModel.EnergyModel.ToString());
                sw.WriteLine("# Lag window: [{0}]", this.ForecastModel.LagWindow);
                sw.WriteLine("# Window size: [{0}]", this.ForecastModel.WindowSize);
                sw.WriteLine("#");
                sw.WriteLine("# Formula: Energy = {0} * Temp^2 + {1} * Temp + {2}", this.ForecastModel.StatsSummary.SquareTermCoefficient, 
                                                                                    this.ForecastModel.StatsSummary.LinearTermCoefficient, 
                                                                                    this.ForecastModel.StatsSummary.Intercept);

                sw.WriteLine("# RSQ = [{0}]", this.ForecastModel.StatsSummary.RSquared);
                sw.WriteLine("# RMSE = [{0}]", this.ForecastModel.StatsSummary.RMSE);
                if (this.ForecastModel.StatsSummary.RSquared < 0.5)
                {
                    sw.Write("# the low RSQ value indicates that this model will not produce a good forecast");
                }
                sw.WriteLine("");

                
                // Next the Header info - essentiall the buildingId
                string headerNames = string.Empty;
                string headerValues = string.Empty;

                foreach (KeyValuePair<string,string> kv in this.Header )
                {
                    headerNames += kv.Key + ", ";
                    headerValues += kv.Value + ", ";
                }

                // strip the trailing comma
                headerNames = headerNames.Substring(0, headerNames.Length - 2);
                headerValues = headerValues.Substring(0, headerValues.Length - 2);

                sw.WriteLine(headerNames);
                sw.WriteLine(headerValues);

                /////////////////////////////////////
                // Next section - the actual results
                /////////////////////////////////////

                // first a comment
                sw.WriteLine("");
                sw.WriteLine("# One energy figure is calculated per day");
                if (this.ForecastModel.EnergyModel == Model.EnergyModelType.DailyTotal)
                {
                    sw.WriteLine("# this is the total energy used for {0} for that day", this.ForecastModel.EnergyFieldName);
                }
                else
                {
                    sw.WriteLine("# this is the average hourly [{0}] energy used for that day whilst the building is operational.", this.ForecastModel.EnergyFieldName);
                }
                sw.WriteLine("");

                sw.WriteLine(T001_timestampUTC + "," + _TemperatureFieldName + "," + _EnergyFieldName);
 
                // to write this we reproduce the input file and write 0, for all times except 00:00
                foreach (KeyValuePair<DateTime, double> kv in this.OATempTS)
                {
                    string line;
                    // Write out data in US format
                    line = kv.Key.ToString("MM/dd/yyyy HH:mm:ss") + "," + kv.Value.ToString();

                    // if midnight time = (00:00) - this is when we write the daily Energy value (either total energy for that day, or average hourly energy (while building is open)
                    if (EnergyTS.Keys.Contains(kv.Key))
                    {

                        line+= "," + EnergyTS[kv.Key].ToString("F2");
                    }
                    else
                    {
                        line+= ",0.0";
                    }
                    sw.WriteLine(line);
                }

                // Done!
                sw.Close();
            }
            catch (Exception ex)
            {
                Logger.LogMessage(MessageType.Error, "Error writing output file [" + _filename + "]: ["+ ex.Message +"]");
                bSuccess = false;

            }
            
            return bSuccess;
        }
    }
#endregion
#region Helper functions
   
#endregion



}
