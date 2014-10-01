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

        // Obsolete
        private const string MAIN_FILE_PREFIX = "MAIN_*";
        private const string SUPP_FILE_PREFIX = "SUPP_*";
        private const string FORECAST_FILE_PREFIX = "PRED_*";
        private const string HOLIDAY_FILE_PREFIX = "HOL_*";

        // Command line Parameters
        private const string CL_PARAM_DEBUG = "/D";
        private const string CL_PARAM_WORKING_WEEK = "/W:";
        private const string CL_PARAM_WORKDAY_END = "/H:";
        private const string CL_PARAM_TRAINING_START = "/S:";
        private const string CL_PARAM_TRAINING_END = "/E:";

        private const int MinNrParameters = 3;
        private const int MaxNrParameters = 7;


        private static ModelEngine me = new ModelEngine();
        private static ModelBuilding building = new ModelBuilding();

        /// <summary>
        /// Main Entry point - 3 or 4 args are expected
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Logger.LogMessage(MessageType.Information, "Program starting");
            
            
            // 1. Check the command line parameters
            // ------------------------------------
            // we need just three:
            //  Trainingfile
            //  Prediction file
            //  Holiday file

            //  /D(ebug) is optional
            
            if (args.Count() > MaxNrParameters || args.Count() < MinNrParameters)
            {
                ShowUsage();
                Environment.ExitCode = ERROR_INVALID_COMMAND_LINE;
                Logger.LogMessage(MessageType.Error, "Invalid number of parameters - Program ending");
                return;
            }



            // 1.1 Check the files exist!
            // --------------------------
            if (!CheckFileExists (args[0],"Training file")||
                !CheckFileExists (args[1],"Forecast file")||
                !CheckFileExists (args[2],"Holiday file"))
            {
                // One or more file doesnt exist - Exiting
                Environment.ExitCode = ERROR_BAD_ARGS;
                return;
            }

            // 1.2 files all exist so we can begin...
            string mainFilename = args[0];
            string forecastFilename = args[1];
            string holidayFilename = args[2];

            List<DayOfWeek> workingDays;
            DateTime workingDayEnd;
            int trainingStartMMDD = -1;
            int trainingEndMMDD = -1;


            // Check for the optional parameters
            if (args.Length > 3) 
            {
                for (int i = 3; i < args.Length; i++ )
                {
                    // 1. Debug enabled?
                    if (args[i].ToUpper().StartsWith(CL_PARAM_DEBUG))
                    {
                        Logger.ShowDebugMesssage = true;
                    }

                    // 2. WorkingWeek
                    if (args[i].ToUpper().StartsWith(CL_PARAM_WORKING_WEEK))
                    {
                        // Correct format is /W:23456
                        workingDays = ParseWorkingDaysParameter(args[i].Substring(CL_PARAM_WORKING_WEEK.Length));
                    }

                    // 3. Time of end of working day
                    if (args[i].ToUpper().StartsWith(CL_PARAM_WORKDAY_END))
                    {
                        // Default if we cant read the command line parameter
                        workingDayEnd = building.WorkdayEndTime;
                        // Correct format is /H:18:15
                        ExtractParameter(args[i].Substring(CL_PARAM_WORKDAY_END.Length), ref workingDayEnd);
                        building.WorkdayEndTime = workingDayEnd;  // set to the extracted value (hopefully) or back to default value if parameter parsing failed
                    }

                    // 4. Training Period Start
                    if (args[i].ToUpper().StartsWith(CL_PARAM_TRAINING_START ))
                    {
                        // Correct format is /S:MMDD
                        if (TryParseMMDD(args[i].Substring(CL_PARAM_WORKING_WEEK.Length), out trainingStartMMDD))
                        {
                            building.TrainingStartMMDD = trainingStartMMDD;
                        }
                        else
                        {
                            // Invalid Parameter value
                            building.TrainingStartMMDD = ModelBuilding.JAN01;

                        }
                    }

                    // 4. Training Period End
                    if (args[i].ToUpper().StartsWith(CL_PARAM_TRAINING_END))
                    {
                        // Correct format is /S:MMDD
                        if (TryParseMMDD(args[i].Substring(CL_PARAM_WORKING_WEEK.Length), out trainingEndMMDD))
                        {
                            building.TrainingEndMMDD = trainingEndMMDD;
                        }
                        else
                        {
                            // Invalid Parameter value
                            building.TrainingEndMMDD = ModelBuilding.DEC31;

                        }
                    }
                }
            }


            // 2. Good to go!
            // --------------
            bool bSuccess = true;

            // Log the parameters used
            Logger.LogMessage(MessageType.Information, "Training file: [" + mainFilename + "]");
            Logger.LogMessage(MessageType.Information, "Forecast file: [" + forecastFilename + "]");
            Logger.LogMessage(MessageType.Information, "Holiday  file: [" + holidayFilename + "]");



            // 2.1 Read the holiday file
            // -------------------------
            // Parse the holiday file and assign to the building
            building.Holidays = ReadHolidayFile(holidayFilename);


            // 2.2 Determine the best model by reading the training data in the main file
            // --------------------------------------------------------------------------
            bSuccess = DetermineBestModel(mainFilename);
            if (bSuccess)
            {
                // Create the forecast based on the best model
                CreateForecastFile(forecastFilename);
            }

            // 3. Run the Model on the files - this creates the output file(s) too
            Logger.LogMessage(MessageType.Information, "Program ending");
        }

        private static bool TryParseMMDD(string mmddAsString, out int result)
        {
            bool bSuccess = true;

            if (int.TryParse(mmddAsString, out result))
            {
                // Check that its a valid format - we dont really care too much about 30 vs 31
                if (result < 101 || result / 100 > 12 || result % 100 > 31)
                {
                    bSuccess = false;
                }
            }

            return bSuccess;
        }



        /// <summary>
        /// Show use the correct usage
        /// </summary>
        static void ShowUsage()
        {
            Console.WriteLine("Usage:"
                +"The executable takes three mandatory command line parameters and one optional parameter:"
                +"\n\t-	Training filename (full pathname)"
                +"\n\t-	Prediction filename (full pathname)"
                +"\n\t-	Holiday filename (full pathname)"
                +"\n\t-	[optional] /D – output debug messages"
                +"\n\nThe output file is written to the same directory as the prediction file.  Output filename is YYYY.MM.DD.Predictionfilename."
                +"\n\ne.g. PSModel.exe \"..\\AutoMV_LBNL\\ModelInput6T.csv\" \"..\\AutoMV_LBNL\\ModelInput6P.csv\" \"..\\AutoMV_LBNL\\USFederalHolidays.csv\""
                );
        }

        static bool CheckFileExists(string filename, string filetypeName)
        {
            bool bSuccess = true;

            if (!System.IO.File.Exists(filename))
            {
                string message = filetypeName + " does not exist[";
                message += filename + "] - Program ending";
                Logger.LogMessage(MessageType.Error, message);
                bSuccess = false;
            }
            return bSuccess;
        }

        private static List<DateTime> ReadHolidayFile(string holidayFilename)
        {
            
            // Read the holiday file
            LBNL_Holiday_File holidayFile = new LBNL_Holiday_File(holidayFilename);
            if (!holidayFile.ParseHolidays())
            {
                string message = "Holiday File [" + holidayFilename + "] has not been read correctly, continuing anyway.";
                Logger.LogMessage(MessageType.Warning, message);
            }
            return holidayFile.Holidays; 

        }

        private static bool  DetermineBestModel(string mainFilename)
        {
            bool bSuccess = true;

            // We need the training info from the main file
            LBNL_Main_File mainFile = new LBNL_Main_File(mainFilename);

            mainFile.StartDateMMDD = building.TrainingStartMMDD;
            mainFile.EndDateMMDD = building.TrainingEndMMDD;
            

            // All dates in the training file are used to train the model as we are currently no permitted to 
            // supply any command line parameters
            // The energy field modelled is the first energy field listed in the training file.


            if (mainFile.ParseHeader())
            {
                // Header OK so let's see how the timeseries looks...
                bSuccess = mainFile.ParseTimeSeries();

                // Training file successfully read - so we can proceed
                if (bSuccess)
                {
                    // Set up our building for analysis
                    building.BuildingId = mainFile.BuildingId;
                    building.NAICSCode = mainFile.BuildingTypeId;
                    building.EnergyFieldname = mainFile.EnergyFieldName;
                        
                    building.IntervalEnergy = mainFile.EnergyTS;
                    building.IntervalTemperatures = mainFile.OATempTS;

                    // Set Working Day end and working week fromcommand line parameters, if supplied
                    //building.
                    building.SetWorkingDaysAccordingtoNAICS();  // M-F or 7/7 for retail

                    // All the modelling work is done in this call
                    bSuccess = building.FindBestModel();

                }
            }
            return bSuccess;
        }
        private static void  CreateForecastFile(string forecastFilename)
            {
                bool bSuccess;
                //  We can do the forecast forecast file
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
                            forecastFile.Building = building;
                            forecastFile.ForecastModel = building.BestModel;    // Unnecessary
                            forecastFile.WriteToFile();
                        }
                    }
                }
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
        private static List<DayOfWeek> ParseWorkingDaysParameter(string workingDaysAsNumericString)
        {

            List<DayOfWeek> _workingDays = new List<DayOfWeek>(workingDaysAsNumericString.Length);

            // 1. all numeric & in range 1..7, 1=Sunday (SQL style)
            foreach (char ch in workingDaysAsNumericString.ToCharArray())
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
                    _workingDays = null;
                }
            }
            return _workingDays;
        }
        private static void ExtractParameter(string parameterValue, ref DateTime result)
        {
            // If field exists, and it has data, we try and parse it
            // insert a : if the user forgot it
            if (parameterValue.Length == 4)
            {
                parameterValue = parameterValue.Substring(0, 2) + ":" + parameterValue.Substring(2);
            }
            if ((parameterValue.Length > 0))
            {
                if (DateTime.TryParse(parameterValue, out result))
                {
                    Logger.LogMessage(MessageType.Information, "Extracted [" + result.ToShortTimeString() + "] from Parameter [" + parameterValue + "]");
                }
                else
                {
                    Logger.LogMessage(MessageType.Warning, "Failed in parse parameter [" + parameterValue + "] correctly, using default: [" + result.ToShortTimeString() + "]");
                }
            }
        }

    }
}
