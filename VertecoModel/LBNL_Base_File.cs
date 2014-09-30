using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Verteco.Shared;

namespace VertecoModel
{
    class LBNL_Base_File
    {
        #region const Field names from LBNL 
        // 
        protected const char SEPARATOR = ',';
        protected const char COMMENT_MARKER = '#';

      


        #endregion
        #region member variables
        protected Dictionary<string, string> _header;
        #endregion
        #region properties

        protected  string _filename;


        public string Filename
        {
            get { return _filename; }
            set { _filename = value; }
        }

        public Dictionary<string, string> Header
        {
            get { return _header ; }
        }
        #endregion
#region construction/destruction
        public LBNL_Base_File(string filename)
        {
            _filename = filename;
        }

        public LBNL_Base_File()
        {

            
        }
        public string[] HeaderValues
        {
            get { return _header.Values.ToArray(); }
        }
#endregion
#region public methods
        /// <summary>
        /// Parse the Main file
        /// </summary>
        /// <returns></returns>
        public bool ParseHeader()
        {
            bool bResult = false;

            try
            {
                if (File.Exists(_filename))
                {
                    // Open the file to read from. 
                    using (StreamReader sr = File.OpenText(_filename))
                    {
                        // Skip the comments until we get to the header
                        string s = "";
                        while ((s = sr.ReadLine()) != null)
                        {
                            if (!IsComment(s))
                            {
                                // Header is the first line with data
                                string[] headers = s.Split(SEPARATOR);
                                string headerdataLine = sr.ReadLine();
                                if (null != headerdataLine)
                                {
                                    // Read header data 
                                    string[] headerData = headerdataLine.Split(SEPARATOR);
                                    _header = CreateDictionaryFromPairs(headers, headerData);

                                    // We should now have all the fields we need
                                    if (null != _header)
                                    {
                                        /////////////////////////////////
                                        // SUCCESS!! - Log the values read
                                        /////////////////////////////////

                                        Logger.LogMessage(MessageType.Information, "Header successfully parsed for ["+ this.Filename +"]");
                                        string message = "The following parameters have been read: ";
                                        foreach (KeyValuePair<string, string> kvp in _header)
                                        {
                                            message += "\n\t[" + kvp.Key + "]=[" + kvp.Value + "]";
                                        }
                                        Logger.LogMessage(MessageType.Information, message);

                                        bResult = true;
                                        break;  // We have what we came for - skip the rest of the file
                                    }
                                    else
                                    {
                                        // Bad File format, detail message alread logged
                                        Logger.LogMessage(MessageType.Error, "Bad Header, quiting");
                                        bResult = false;
                                    }
                                }
                                else
                                {
                                    // file format error
                                    Logger.LogMessage(MessageType.Error, "Bad Header formatting, quiting");
                                    bResult = false;
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

            }
            catch (Exception ex)
            {
                    string message;
                    message = "Exception occruured parsing Header [" + ex.Message + "]";
                    Logger.LogMessage(MessageType.Error, message);
                    bResult = false;
            }
            return bResult;
        }
#endregion
#region Helper functions
        protected bool IsComment(string s)
        {
            bool bResult = true;

            bResult = (0 == s.Length || COMMENT_MARKER == s[0]);
            
            return bResult;
        }

        protected Dictionary<string, string> CreateDictionaryFromPairs(string[] fieldNames, string[] fieldValues)
        {
           
            Dictionary<string, string> results;

            if (fieldNames.Length == fieldValues.Length)
            {
                // File format error - different number of field values
                // (Empty fields and NA fields are permitted)
                results =  new Dictionary<string, string>(fieldValues.Length);
                for (int i = 0; i < fieldValues.Length; i++)
                {
                    // fieldnames are case insensitive - we remove leading/trailing spaces and store Keys as uppercase for clarity/ease of programming
                    if (fieldNames[i].ToUpper().Trim().Length > 0)
                    {
                        results.Add(fieldNames[i].ToUpper().Trim(), fieldValues[i].Trim());
                    }
                }
            }
            else
            {
                // Log Error & return null
                Logger.LogMessage (MessageType.Error,"Nr fields specified does not match data: Nr Fields:["+fieldNames.Length +"] Nr Data Values:["+fieldValues.Length +"]");
                results = null;
            }


            return results;
        }
#endregion

    }

}
