using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verteco.Shared;

namespace VertecoModel
{
    class ModelEngine
    {
        #region private member variables
        private Dictionary<DateTime, double> _dailyEnergy;
        private Dictionary<DateTime, double> _dailyEnergyPerHour;

        // Temperatures - array of daily average lagged temperatures, one for each window size
        private Dictionary<DateTime, double> _dailyAverageTemp;
        #endregion
        #region Properties
        public List<DateTime> NonWorkingDays { get; set; }
        public double EnergyThreshold { get; set; }
        #endregion
        #region Construction/Destruction
        public ModelEngine()
        {
            NonWorkingDays = new List<DateTime>();
        }
        #endregion
        #region methods which work on the raw data
        public Dictionary<DateTime, double> CalculateDailyAverageTemperatures(  Dictionary<DateTime, double> sourceData,
                                                        DateTime closingTime,
                                                        int windowSizeInHours,
                                                        double lagInHours)
        {
            TimeSpan  windowStart;
            TimeSpan windowEnd;

            Dictionary<DateTime, double> calculatedAverages = new Dictionary<DateTime,double>();


            // 3. next up we calculate the start & endtimes
            windowStart = closingTime.AddHours( -lagInHours-windowSizeInHours ).TimeOfDay ;
            windowEnd = closingTime.AddHours(-lagInHours ).TimeOfDay ;

            // MAKE SURE WINDOW START IS NOT ON PREVIOUS DAY 
            // TODO this should be refactored so that data from previous day can be included in window
            if (windowEnd < windowStart)
            {
                windowStart = new TimeSpan(0, 0, 0);
            }

            // Data has already been validated, (no larges holes)
            // LINQ is our friend!
            var res = from t in sourceData
                      where t.Key.TimeOfDay >= windowStart && t.Key.TimeOfDay <= windowEnd
                            && !NonWorkingDays.Contains(t.Key.Date) // exclude holidays
                            && !double.IsNaN ( t.Value)             // exclude NaNs from calculations TODO - there's a threshold of NaNs above which data should be discounted
                      group t by t.Key.Date into daily
                      select new { Date = daily.Key, AvgTemp = daily.Average(x => x.Value) };

            foreach (var result in res)
            {
                calculatedAverages.Add(result.Date, result.AvgTemp);
            }

            return calculatedAverages;
        }
        public Dictionary<DateTime, double> CalculateDailyTotalEnergy(Dictionary<DateTime, double> sourceData)
        {

            Dictionary<DateTime, double> dailyEnergy = new Dictionary<DateTime, double>();
            
            var res = from t in sourceData
                      where t.Value > EnergyThreshold
                            && !NonWorkingDays.Contains(t.Key.Date) // exclude holidays
                             && !double.IsNaN(t.Value) // exclude NaNs from calculations TODO - there's a threshold of NaNs above which data should be discounted
                      group t by t.Key.Date into daily
                      select new { Date = daily.Key, DailyEnergy = daily.Sum(x => x.Value) };

            foreach (var result in res)
            {
                dailyEnergy.Add(result.Date, result.DailyEnergy);
            }

            return dailyEnergy;
        }



        public Dictionary<DateTime, double> CalculateAverageHourlyEnergy(Dictionary<DateTime, double> sourceData)
        {

            Dictionary<DateTime, double>  dailyEnergyPerHour = new Dictionary<DateTime, double>();

            var res = from t in sourceData
                      where t.Value > EnergyThreshold
                            && !NonWorkingDays.Contains(t.Key.Date) // exclude holidays
                      group t by t.Key.Date into daily
                      select new { 
                            Date = daily.Key , 
                            AverageHourlyEnergy = daily.Sum(x => x.Value)/(daily.Max(x => x.Key).Ticks  - daily.Min(x => x.Key).Ticks)*TimeSpan.TicksPerHour };
            
            foreach (var result in res)
            {
                dailyEnergyPerHour.Add(result.Date, result.AverageHourlyEnergy);
            }

            return dailyEnergyPerHour;
        }
        #endregion
        #region Statistical Methods
        public ModelStatistics PerformLinearRegresssion(Dictionary<DateTime, double> dailyTemperatures, Dictionary<DateTime, double> dailyEnergy)
        {
           
            // first we make sure that the date ranges correspond and the datasets are the same size
            // unfortunately, as there may be gaps in with dataset (temps or energy) we need to go through them day by day


            // 1. Both lists need to be made the same size
            List<double> temperatures = new List<double>(dailyTemperatures.Count);
            List<double> energy = new List<double>(dailyEnergy.Count);

            foreach (DateTime dt in dailyTemperatures.Keys)
            {
                if (dailyEnergy.Keys.Contains(dt))
                {
                    bool badData;
                    badData = double.IsInfinity(dailyTemperatures[dt]) || double.IsNaN(dailyTemperatures[dt]) || double.IsInfinity(dailyEnergy[dt]) || double.IsNaN(dailyEnergy[dt]);

                    if (!badData)
                    {
                        temperatures.Add(dailyTemperatures[dt]);
                        energy.Add(dailyEnergy[dt]);
                    }
                    else
                    {
                        Logger.LogMessage(MessageType.Warning, "Omitting data from ["+dt.ToString("yyyy-MM-dd") +"]");
                    }
                }
            }
                
            // This pair of datasets is now ready for statistical analysis - lets do it! 
            ModelStatistics result =  RWrapper.PerformLinearRegression(temperatures, energy);

            return result;
        }


        public Dictionary<DateTime, double> CreateForecast(Model usingThisModel, Dictionary<DateTime, double> _inputTemperatures)
        {
            Dictionary<DateTime, double> _outputEnergy = new Dictionary<DateTime, double>();
            
            // first we need to work out the daily average temperatures accoring to the Lag & window in the Model
            Dictionary<DateTime, double> averageDailyTemps = new Dictionary<DateTime, double>();

            averageDailyTemps = CalculateDailyAverageTemperatures (_inputTemperatures, usingThisModel.WorkingDayEndTime,usingThisModel.WindowSize, usingThisModel.LagWindow );

            // We use both Total & Hourly - why not!
            foreach(KeyValuePair<DateTime, double>  temperature in averageDailyTemps)
            {
                // One per day
                double averageDailyEnergy = usingThisModel.StatsSummary.Intercept + temperature.Value * usingThisModel.StatsSummary.LinearTermCoefficient + temperature.Value * temperature.Value * usingThisModel.StatsSummary.SquareTermCoefficient;
                _outputEnergy.Add(temperature.Key, averageDailyEnergy);
            }

            return _outputEnergy;

        }

        private ModelStatistics PerformLinearRegressionOnModel(Model model, Dictionary<DateTime, double> energy)
        {
            // Both lists for the RWrapper need to be made the same size
            List<double> temperatures = new List<double>(model.DailyAverageTemperature.Count);
            List<double> dailyEnergy = new List<double>(model.DailyAverageTemperature.Count);

            foreach (DateTime dt in model.DailyAverageTemperature.Keys)
            {
                if (energy.Keys.Contains(dt))
                {
                    temperatures.Add(model.DailyAverageTemperature[dt]);
                    dailyEnergy.Add(energy[dt]);
                }
            }

            // This pair of datasets is now ready for statistical analysis - lets do it! 
            ModelStatistics result = RWrapper.PerformLinearRegression(temperatures, dailyEnergy);

            return result;
        }


        #endregion
    }
}
