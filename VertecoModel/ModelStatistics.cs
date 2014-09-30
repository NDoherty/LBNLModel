using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VertecoModel
{
    class ModelStatistics
    {
        // Final Results
        public double RSquared { get; set; }
        public double RMSE { get; set; }

        // The equation
        public double Intercept { get; set; }
        public double LinearTermCoefficient { get; set; }
        public double SquareTermCoefficient { get; set; }

    }
}
