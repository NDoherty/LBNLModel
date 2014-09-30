using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using Verteco.Shared;
 
namespace VertecoModel
{
    class Program
    {
        // Error codes
        private const int ERROR_INVALID_COMMAND_LINE = 0xA0;
        private const int ERROR_BAD_ARGS = 0xA1;

        private const string MAIN_FILE_PREFIX = "MAIN_*";
        private const string SUPP_FILE_PREFIX = "SUPP_*";
        private const string FORECAST_FILE_PREFIX = "PRED_*";
 
        private const int MinNrParameters = 3;
        private const int MaxNrParameters = 4;

        static void Main(string[] args)
        {
            Logger.LogMessage(MessageType.Information, "Program starting");
            // 1. Check the command line parameters
            // we need just three:
            //  Trainingfile
            //  Prediction file
            //  Holiday file
            
            if (args.Count() > MaxNrParameters || args.Count() < MinNrParameters)
            {
                ShowUsage();
                Environment.ExitCode = ERROR_INVALID_COMMAND_LINE;
                Logger.LogMessage(MessageType.Error, "Invalid number of parameters - Program ending");
                return;
            }

            // Otherwise we're off and running
            // (This could be moved to separate fn)

            // 1. Check parameters (does input exist)
            // 
            if (!System.IO.File.Exists(args[0]))
            {
                Environment.ExitCode = ERROR_BAD_ARGS;
                string message = "Training file does not exist[";
                message += args[0] + "] - Program ending";
                Logger.LogMessage(MessageType.Error, message);
                return;

            }

            if (!System.IO.File.Exists(args[1]))
            {
                Environment.ExitCode = ERROR_BAD_ARGS;
                string message = "Prediction file does not exist[";
                message += args[1] + "] - Program ending";
                Logger.LogMessage(MessageType.Error, message);
                return;

            } 
            if (!System.IO.File.Exists(args[2]))
            {
                Environment.ExitCode = ERROR_BAD_ARGS;
                string message = "Holiday file does not exist[";
                message += args[2] + "] - Program ending";
                Logger.LogMessage(MessageType.Error, message);
                return;

            }

            if (args.Length == 4 && args[3].ToUpper().Contains("/D"))
            {
                Logger.ShowDebugMesssage = true;
            }

            // Find our files - we know we have them all from above so no need to check existence
            string mainFilename = args[0];
            string forecastFilename = args[1];
            string holidayFilename = args[2];

            bool bSuccess = true;

           
            // Log the parameters used
            Logger.LogMessage(MessageType.Information, "Training file: [" + mainFilename + "]");
            Logger.LogMessage(MessageType.Information, "Forecast file: [" + forecastFilename + "]");
            Logger.LogMessage(MessageType.Information, "Holiday  file: [" + holidayFilename + "]");

            // 2. Check for existence of input files 
            // There should be a main file a supplemental file and 
            // 
            if (bSuccess)
            {
                //////////////////////////////////////////////////////////////////////////////////////////////////////
                // NO SUPPLEMENTARY FILE REQUIRED
                // good to go again.
                //////////////////////////////////////////////////////////////////////////////////////////////////////
                // we first parse the supplemental file as that contains parameters we need to analyse the main file
                //LBNL_Supp_File suppFile = new LBNL_Supp_File(suppFilename);
                //if (suppFile.ParseHeader())
                //{
                //    // File is formatted correctly.  Now we check that all mandatory fields are present
                //    if (suppFile.CheckFile())
                //    {
                //        // File content is all good so we can use it to parse the main file
                //        // First we log the parameters read.
                //        string message = "Supplemental File ["+suppFilename+"] has been successfully read.";
                //        Logger.LogMessage(MessageType.Information, message);

                //        // now we need to extract the important 
                //        bSuccess = true;

                //    }
                //}
                //////////////////////////////////////////////////////////////////////////////////////////////////////

                //TODO - New function for this
                ModelEngine me = new ModelEngine();
                ModelBuilding building = new ModelBuilding();


                // Main File
                LBNL_Main_File mainFile = new LBNL_Main_File(mainFilename);

                // All dates in the training file are used to train the model as we are currently no permitted to 
                // supply any command line parameters
                // The energy field modelled is the first energy field listed in the training file.

                // the f
                //mainFile.StartDateMMDD = int.Parse(suppFile.TrainingSeasonStartDateMMDD);
                //mainFile.EndDateMMDD = int.Parse(suppFile.TrainingSeasonEndDateMMDD);
                //mainFile.EnergyFieldName = suppFile.EnergyToModel;
             

                if (bSuccess && mainFile.ParseHeader())
                {
                    
                    // Log something
                    bSuccess = mainFile.ParseTimeSeries();

                    // Read the holiday file
                    LBNL_Holiday_File holidayFile = new LBNL_Holiday_File(holidayFilename);
                    if (holidayFile.ParseHolidays())
                    {
                        string message = "Holiday File [" + holidayFilename + "] has been successfully read.";
                        Logger.LogMessage(MessageType.Information, message);
                    }

        

                    if (bSuccess)
                    {

                        // Set up our building for analysis
                        building.BuildingId = mainFile.BuildingId;
                        building.NAICSCode = mainFile.BuildingTypeId;
                        building.EnergyFieldname = mainFile.EnergyFieldName;
                        building.Holidays = holidayFile.Holidays;
                        
                        building.IntervalEnergy = mainFile.EnergyTS;
                        building.IntervalTemperatures = mainFile.OATempTS;

                        building.SetWorkingDaysAccordingtoNAICS();
                        
                        bSuccess = building.CalculateDailyEnergy();
                        
                        // We iterate through lags from 0 to 8 hours, window sizes from 8 to 16 and workingDay end time is 18:00
                        // In future these could be command line parameters, or parameters in a supplemental file
                        double minLag, maxLag;
                        int minWindowSize, maxWindowSize;
                        DateTime workingDayEndTime = DateTime.Parse("18:00:00");
                        minLag = 0.0;
                        maxLag = 8.0;
                        minWindowSize = 8;
                        maxWindowSize = 16;

                        string message = "Modelling process is starting...";
                        Logger.LogMessage(MessageType.Information, message);

                        double currentLag = minLag ;
                        while  (currentLag <= maxLag)
                        {
                            message = "Modelling with NTL of " + currentLag.ToString() +"] hours."; 
                            Logger.LogMessage(MessageType.Information, message);
                            building.FindBestModel(minWindowSize, maxWindowSize, workingDayEndTime, currentLag);
                            currentLag += 0.25;
                        }
                        // Log Best model and use it to forecast
                        message = "Modelling completed";
                        Logger.LogMessage(MessageType.Information, message);

                        message = "Best Model:: WindowSize = [" + building.BestModel.WindowSize.ToString() + " QH periods] " +
                                    "Lag Window = " + building.BestModel.LagWindow.ToString() + " hours] " +
                                    "Model Type = " + building.BestModel.EnergyModel.ToString() + "] "+
                                    "RSQ = " + building.BestModel.StatsSummary.RSquared  +"]";
                        Logger.LogMessage(MessageType.Information, message);

                    }
                }
           
         
                // 2.1. Check for existence of forecast file
                if (bSuccess  && null != building.BestModel)
                {
                    //  open forecast file
                    LBNL_Forecast_File forecastFile = new LBNL_Forecast_File(forecastFilename, building.EnergyFieldname );

                    // Cooling as a test TODO, either do this (heating vs cooling) is the supp file or here


                    if (forecastFile.ParseHeader())
                    {
                        // Log something
                        bSuccess = forecastFile.ParseTimeSeries();
                        if (bSuccess)
                        {
                            // we have temperature data - we just need to use that to predict the energy usage!
                            forecastFile.EnergyTS =  me.CreateForecast(building.BestModel, forecastFile.OATempTS);
                            if (null != forecastFile.EnergyTS)
                            {
                                forecastFile.ForecastModel = building.BestModel;
                                forecastFile.WriteToFile();
                            }
                        }
                    }
                }
            }
            // 3. Run the Model on the files - this creates the output file(s) too
            Logger.LogMessage(MessageType.Information, "Program ending");
        }



        /// <summary>
        /// Show use the correct usage
        /// </summary>
        static void ShowUsage()
        {
            Console.WriteLine("TODO:: Usage");
        }

        static bool TryFindFirstMatchingFile(string directory, string pattern, out string filename)
        {
            bool bSuccess = true;
            string errorMessage;
            
            filename = "";  // not found

            IEnumerable<string> files = System.IO.Directory.EnumerateFiles(directory, pattern );
            if (files.Count() > 0)
            {
                filename = files.First();
            }
            else
            {
                // main file is missing
                errorMessage = "No matching files found: [" + pattern  + "] in directory [" + directory + "].";
                Logger.LogMessage(MessageType.Error, errorMessage);
                bSuccess = false;
            }
            return bSuccess;
        }
    }
}
