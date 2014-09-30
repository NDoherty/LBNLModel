using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verteco.Shared;

namespace VertecoModel
{
    /// <summary>
    /// This is the basis for the refactored code
    /// TODO
    /// </summary>
    class ModelBuilding
    {
        private const string NAICS_RETAIL1 = "44";
        private const string NAICS_RETAIL2 = "45";
        private const int DEFAULT_ENERGY_THRESHOLD = 1;

        /////////////////////////////////
        // Building Characteristics first
        /////////////////////////////////
        /// <summary>
        /// 
        /// </summary>
        public string BuildingId { get; set; }
        public string NAICSCode { get; set; }
        public  List<DayOfWeek> WorkingDays;
        /// <summary>
        /// Nonworking days (holidays) are stored directly in the model engine
        /// </summary>
        public List<DateTime> Holidays { set { if (null != modelEngine) modelEngine.NonWorkingDays = value; } }
        /// <summary>
        /// The energy threshold (if energy < this level then relevant plant is OFF) is also stored in the modelEngine
        /// </summary>
        public double EnergyThreshold { set { if (null != modelEngine) modelEngine.EnergyThreshold = value; } }

        ////////////////////
        // Model Parameters
        ////////////////////
        /// <summary>
        /// 
        /// </summary>
        public string EnergyFieldname { get; set; }

        ////////////////////
        // Source data
        ////////////////////
        public Dictionary<DateTime, double> IntervalEnergy { get; set; }
        public Dictionary<DateTime, double> IntervalTemperatures { get; set; }

        ////////////////////
        // Derived Data
        ////////////////////
        private Dictionary<DateTime, double> _dailyEnergy;
        private Dictionary<DateTime, double> _dailyEnergyPerHour;

        ////////////////////
        // Models
        ////////////////////
        private List<Model> _allModels;

        public Model BestModel {get; set;}
        public Model ForecastModel {get; set;}

        private ModelEngine modelEngine = new ModelEngine();


        public ModelBuilding()
        {
            _allModels = new List<Model>();
            this.EnergyThreshold = DEFAULT_ENERGY_THRESHOLD;

        }

        public bool CalculateDailyEnergy()
        {
            _dailyEnergy = modelEngine.CalculateDailyTotalEnergy(IntervalEnergy);
            _dailyEnergyPerHour = modelEngine.CalculateAverageHourlyEnergy (IntervalEnergy);
            if ((null != _dailyEnergy) || (null != _dailyEnergyPerHour))
            {
                Logger.LogMessage(MessageType.Information, "Successfully calculated daily energy");
            }
            return (null != _dailyEnergy) || (null != _dailyEnergyPerHour);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="minWindowSize"></param>
        /// <param name="maxWindowSize"></param>
        /// <param name="workingDayEndTime"></param>
        /// <param name="lagWindow"></param>
        public void FindBestModel(int minWindowSize, int maxWindowSize, DateTime workingDayEndTime, double lagWindow)
        {
            Dictionary<DateTime , double> averageDailyTemps;

            for (int window = minWindowSize; window <= maxWindowSize; window++)
            {
                averageDailyTemps = modelEngine.CalculateDailyAverageTemperatures(IntervalTemperatures, workingDayEndTime, window, lagWindow);
                if (null != averageDailyTemps)
                {
                    //
                    Logger.LogMessage(MessageType.Debug, "Successfully calculated average daily temperatures");
                    //
                    Model newModel = new Model(lagWindow, window);
                    // Copy 
                    newModel.EnergyFieldName = this.EnergyFieldname;
                    newModel.WorkingDayEndTime = workingDayEndTime;
                    newModel.DailyAverageTemperature = averageDailyTemps;
                    _allModels.Add(newModel);

                    // Perform the regression against daily total energy & 

                    ModelStatistics resultVsTotalDailyEnergy = modelEngine.PerformLinearRegresssion(averageDailyTemps, _dailyEnergy);
                    ModelStatistics resultVsAverageHourlyEnergy = modelEngine.PerformLinearRegresssion(averageDailyTemps, _dailyEnergyPerHour);

                    // This is the most straightfoward way to deal with null results
                    if (null == resultVsTotalDailyEnergy)
                    {
                        resultVsTotalDailyEnergy = new ModelStatistics();
                        resultVsTotalDailyEnergy.RSquared = 0.0;
                    }
                    if (null == resultVsAverageHourlyEnergy)
                    {
                        resultVsAverageHourlyEnergy = new ModelStatistics();
                        resultVsAverageHourlyEnergy.RSquared = 0.0;
                    }

                    Logger.LogMessage(MessageType.Debug, "Linear Regression completed for Window:[" + window.ToString() + "].  Lag:[" + lagWindow.ToString() + "]");
                    Logger.LogMessage(MessageType.Debug, "RSQ Values: Daily=[" + resultVsTotalDailyEnergy.RSquared.ToString("F6") + "].  Hourly=[" + resultVsAverageHourlyEnergy.RSquared.ToString("F6") + "]");

                    // Now go compare!
                    // First we work out which set of energy measurements gave the best result
                    if (resultVsTotalDailyEnergy.RSquared >= resultVsAverageHourlyEnergy.RSquared)
                    {
                        newModel.EnergyModel = Model.EnergyModelType.DailyTotal;
                        newModel.StatsSummary = resultVsTotalDailyEnergy;
                    }
                    else
                    {
                        newModel.EnergyModel = Model.EnergyModelType.AverageHourly;
                        newModel.StatsSummary = resultVsAverageHourlyEnergy;
                    }


                    // There's really no need to store the other models in the _allmodels array - may remove this in final release
                    // Average temps calculated - lets calculate the Correlation and see if its better than we already have (RSQ only)
                    if (null == BestModel || newModel.StatsSummary.RSquared > BestModel.StatsSummary.RSquared)
                    {
                        BestModel = newModel;
                    }
                   
                }
            } // end for window size
        }

        internal void SetWorkingDaysAccordingtoNAICS()
        {
            // min is 5 days
            WorkingDays = new List<DayOfWeek>(5);
            WorkingDays.Add(DayOfWeek.Monday);
            WorkingDays.Add(DayOfWeek.Tuesday);
            WorkingDays.Add(DayOfWeek.Wednesday);
            WorkingDays.Add(DayOfWeek.Thursday);
            WorkingDays.Add(DayOfWeek.Friday);

            // Retailers get Sat/Sun also
            if (this.NAICSCode.Length > 2)
            {
                if (this.NAICSCode.Substring(0, 2).Equals(NAICS_RETAIL1) || this.NAICSCode.Substring(0, 2).Equals(NAICS_RETAIL2))
                {
                    WorkingDays.Add(DayOfWeek.Saturday);
                    WorkingDays.Add(DayOfWeek.Sunday);
                }
                
            }
        }
    }
}
