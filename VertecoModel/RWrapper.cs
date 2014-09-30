using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// 3rd party 
using RDotNet;

// Verteco
using Verteco.Shared;

namespace VertecoModel
{
    /// <summary>
    /// Class to allow easy access to R Library
    /// </summary>
    static class RWrapper
    {
        static REngine r;
        static bool bREngineReady = true;
        /// <summary>
        /// Constructor - attempts to find the REngine
        /// This should be moved to a separate Init() Method as exceptions should not be thrown in constructors
        /// </summary>
        static RWrapper()
        {

            try
            {
                REngine.SetEnvironmentVariables();
                r = REngine.GetInstance();

            }
            catch (Exception ex)
            {
                Logger.LogMessage(MessageType.Error, "Failed to initialise R-engine. ["+ ex.Message +"] EXITING.");
                bREngineReady = false;
                throw;
            }
        }

        /// <summary>
        /// PerformLinearRegression
        /// Regresses energy series against the temperatures series (both are enumerable)
        /// Note that this is actually more generic really regressing Series A vs Series B
        /// </summary>
        /// <param name="temperatures"></param>
        /// <param name="energy"></param>
        /// <returns></returns>
        static public ModelStatistics  PerformLinearRegression(IEnumerable<double> temperatures, IEnumerable<double> energy)
        {
            bool bSuccess = true;

            if (!bREngineReady)
            {
                Logger.LogMessage(MessageType.Error, "PerformLinearRegression::  R-engine not initialised. EXITING.");
                return null;
            }

            ModelStatistics result = new ModelStatistics();
            
            try
            {
                NumericVector temperatureVector = r.CreateNumericVector(temperatures);
                NumericVector energyVector = r.CreateNumericVector(energy);

                r.SetSymbol("temps", temperatureVector);
                r.SetSymbol("energy", energyVector);

                // --------------------------------------------------------------------------------------
                // USE R to generate a linear regression model.  
                // Evaluate() method allows us to run R command as if we are typing on the R command line
                //
                // the ~temps+I(temps^2) bit means that we're looking for a polynomial best fit (order of 2)
                // ~temps gives use a linear best fit
                // --------------------------------------------------------------------------------------

                // This performs the regression and place the results into the R variable named "results"
                GenericVector testResult = r.Evaluate("results <-lm(formula= energy ~temps+I(temps^2))").AsList();

                // read the RSQ result back to C#  - "return the [$r.squared] field of the vector created when the summary function is executed on the results Vector"
                GenericVector rsqV = r.Evaluate("summary(results)$r.squared").AsList();

                // RSQ is the first item in the returned vector (it's the only one we asked for!)
                double rsq;
                if (null != rsqV && rsqV.Length > 0)
                {
                    result.RSquared = rsqV.AsNumeric().First();
                }
                else
                {
                    Logger.LogMessage(MessageType.Warning, "R-Engine Error:: Failed to read RSQ");
                }

                // Read the coefficients from the results vector 
                GenericVector coefs = r.Evaluate("results$coefficients").AsList();

                double intercept;
                double slope;
                double slope2;

                if (null != coefs && coefs.Length > 2)
                {
                    intercept = coefs[0].AsNumeric().First();
                    slope = coefs[1].AsNumeric().First();
                    slope2 = coefs[2].AsNumeric().First();
                }
                else
                {
                    // Should we just let our exception handler do all this error handling?
                    Logger.LogMessage(MessageType.Warning, "R-Engine Error:: Failed to read Coefficients");
                }

                try
                {

                    // find the rmse - requires package "Metrics"  The vector returned contains a list of the function
                    // This throws an exceptionif the library isnt installed
                    GenericVector loadMetrics = r.Evaluate("library(Metrics)").AsList();

                    // We use the rmse function from the metrics library to calculate the RMSE
                    var rmseV = r.Evaluate("rmse(energy, results$fitted)");

                    // Right, that's all the R done - now just copy the results and return
                    result.RMSE = rmseV.AsNumeric().First();
                }
                catch (Exception ex)
                {
                    // NO Metric library => no RMSE
                    Logger.LogMessage(MessageType.Warning, "R-Engine Error:: Failed to load Metrics Library [" + ex.Message+"]");

                }

                result.Intercept = coefs[0].AsNumeric().First();
                result.LinearTermCoefficient = coefs[1].AsNumeric().First();
                result.SquareTermCoefficient = coefs[2].AsNumeric().First();
            }
            catch (Exception ex)
            {
                Logger.LogMessage(MessageType.Warning, "Exception calling R:: [" + ex.Message + "]");
                result = null;
            }

            return result;
        }
    }
}
