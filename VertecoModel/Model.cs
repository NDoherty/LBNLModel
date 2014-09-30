using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VertecoModel
{
    /// <summary>
    /// This class holds the intermediate and final results for an analysis
    /// </summary>
    class Model
    {
        public enum EnergyModelType { DailyTotal, AverageHourly};
        // Parameters used
        public double LagWindow { get; set; }
        public int WindowSize { get; set; }

        // might drop these from this class as no need (except for reporting)
        public EnergyModelType EnergyModel { get; set; }
        public string EnergyFieldName { get; set; }
        public DateTime WorkingDayEndTime { get; set; }

        // Statistical results
        public Model(double lagWindow, int windowsize)
        {
            LagWindow = lagWindow;
            WindowSize = windowsize;
        }
        // Intermediate results
        // first two are same for all results - so separate lists?
        // public Dictionary<int, double> DailyEnergy;
        // public Dictionary<int, double> DailyEnergyPerHour;

        public Dictionary<DateTime, double> DailyAverageTemperature = new Dictionary<DateTime,double>();

        public ModelStatistics StatsSummary = new ModelStatistics();

    }
}
