﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;


namespace ServicePublisher.MXD
{
    public class PublishMXD
    {
        private string _sMXDTemplate;
        private string _ArcGISServer;
        private string _sMapServiceName;
        private string _sOutputDirectory;
        private bool _bSDE;
        private string _sDBConn;
        private string _sDataset;

        public PublishMXD(string sMXDTemplate, string ArcGISServer, string sServiceName, string sDBConn, string sDataset, string sOutputDirectory, bool bSDE)
        {
            _sMXDTemplate = sMXDTemplate;
            _ArcGISServer = ArcGISServer;
            _sMapServiceName = CleanMXDName(sServiceName);
            _sOutputDirectory = sOutputDirectory;
            _sDBConn = sDBConn;
            _sDataset = sDataset;
            _bSDE = bSDE;
        }

        /// <summary>
        /// Publishes a Map Service using ArcGIS 10
        /// </summary>
        /// <returns>Published URL or Error as a string</returns>
        public string Publish()
        {
            int minInstances = 0;
            int maxInstances = 2;
            int waitTimeout = 60;
            int usageTimeout = 60;
            int idleTimeout = 60;

            return Publish(minInstances, maxInstances, waitTimeout, usageTimeout, idleTimeout, "", "");
        }

        /// <summary>
        /// Passes Ags Connection file and Python Script to publish using ArcGIS 10.1
        /// </summary>
        /// <param name="sAgsConnFile"></param>
        /// <param name="sPythonScriptFile"></param>
        /// <returns>Published URL or Error as a string</returns>
        public string Publish(string sAgsConnFile, string sPythonScriptFile)
        {
           return Publish(0, 0, 0, 0, 0, sAgsConnFile, sPythonScriptFile);
        }

        /// <summary>
        /// Publishes a Map Service
        /// </summary>
        /// <param name="minInstances"></param>
        /// <param name="maxInstances"></param>
        /// <param name="waitTimeout"></param>
        /// <param name="usageTimeout"></param>
        /// <param name="idleTimeout"></param>
        /// <param name="sAgsConnFile"></param>
        /// <param name="sPythonScriptFile"></param>
        /// <returns>Published URL or Error as a string</returns>
        public string Publish(int minInstances, int maxInstances, int waitTimeout, int usageTimeout, int idleTimeout, string sAgsConnFile, string sPythonScriptFile)
        {
            string sURLs = string.Empty;
            string sMapServiceName = string.Empty;
            string sMxdFile = string.Empty;
            string sMxdTemplateName = string.Empty;
            MxdManager pManager = null;

            try
            {
                if (String.IsNullOrEmpty(_sMXDTemplate)) return "Error: No MXD passed.";
                
                pManager = new MxdManager(_sMXDTemplate);
                
                if (pManager.IsInitialized)
                {
                    // Set sMapServiceName
                    sMapServiceName = _sMapServiceName;

                    // Set sMxdTemplateName
                    _sMXDTemplate = _sMXDTemplate.Replace("C:\\\\", "C:\\");
                    sMxdTemplateName = _sMXDTemplate.Substring(_sMXDTemplate.LastIndexOf("\\") + 1);
                    sMxdTemplateName = sMxdTemplateName.Remove(sMxdTemplateName.IndexOf(".mxd"));

                    // Set sMxdFile
                    sMxdFile = Path.Combine(_sOutputDirectory, sMapServiceName + "_" + sMxdTemplateName + ".mxd");

                    if (!(pManager.CreateMxd(_sMXDTemplate, _sOutputDirectory, _ArcGISServer, sMxdFile, _sDBConn, _sDataset, _bSDE)))
                    {
                        sURLs = sURLs + "MxdManager.CreateMxd failed for " + _sMXDTemplate + ". Please see logfile for details.,";
                    }

                    // If sAgsConnFile is empty then publish using ArcGIS Server 10
                    if (string.IsNullOrEmpty(sAgsConnFile))
                    {
                        sURLs = sURLs + pManager.PublishMap(sMapServiceName + "_" + sMxdTemplateName, _ArcGISServer, sMxdFile, _bSDE, minInstances, maxInstances, waitTimeout, usageTimeout, idleTimeout);
                    }
                    else // publish using ArcGIS Server 10.1
                    {
#if _WIN64 
                        sURLs = sURLs + pManager.PublishMap10_1(sMapServiceName + "_" + sMxdTemplateName, _ArcGISServer, sMxdFile, _sOutputDirectory, sAgsConnFile, sPythonScriptFile);
#endif
                    }
                }

                return sURLs.TrimEnd(',');
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error in PublishMXD.Publish: " + ex.Message + " Stack trace: " + ex.StackTrace);
            }
            finally
            {
                
                pManager = null;
            }
        }

        // TODO: Add 10.1 method
        public string UnPublish()
        {
            string sDeleteFgdbResult = "";
            string sURLs = "";
            string sMapServiceName = "";
            string sMxdName;
            FileInfo fiMxd;
            MxdManager manager = null;
            bool errorOccured = false;

            try
            {
                if (String.IsNullOrEmpty(_sMXDTemplate)) 
                    return "Error: No MXD passed.";
                              
                    sMxdName = "";
                    manager = new MxdManager("");

                    sMapServiceName = _sMXDTemplate;
                    if (!(manager.UnPublishMap(sMapServiceName, _ArcGISServer, _sOutputDirectory, _sDataset, _sDBConn)))
                    {
                        sURLs = sURLs + "Error: MxdManager.UnPublishMap failed for " + sMapServiceName + ". Please see logfile for details.,";
                        errorOccured = true;
                    }
                    else
                    {
                        sURLs = sURLs + "Removal of " + sMapServiceName + " was successful.,";
                    }                

                // Remove trailing ','
                sURLs = sURLs.Remove(sURLs.Length - 1);

                if (!(errorOccured))
                {
                    //if locks exist only path is passed in
                    if (_sDBConn.Contains(".gdb"))
                    {
                        sDeleteFgdbResult = manager.DeleteGdb(_sDBConn);
                        if (sDeleteFgdbResult.Length > 0) sURLs = sURLs + "," + sDeleteFgdbResult;
                    }
                }
                return sURLs;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error in PublishMXD.UnPublish: " + ex.Message + " Stack trace: " + ex.StackTrace);
            }
            finally
            {
                fiMxd = null;
                manager = null;
            }
        }

        #region Privates
        private string CleanMXDName(string sMXDName)
        {
            sMXDName = sMXDName.Replace(" ", "_");
            sMXDName = sMXDName.Replace(".", "");
            sMXDName = sMXDName.Replace(",", "");
            sMXDName = sMXDName.Replace("&", "");
            sMXDName = sMXDName.Replace("'", "");
            sMXDName = sMXDName.Replace("\\", "");
            sMXDName = sMXDName.Replace(";", "");
            sMXDName = sMXDName.Replace(":", "");
            sMXDName = sMXDName.Replace("\"", "");
            sMXDName = sMXDName.Replace("|", "");
            sMXDName = sMXDName.Replace("=", "");
            sMXDName = sMXDName.Replace(">", "");
            sMXDName = sMXDName.Replace("<", "");
            sMXDName = sMXDName.Replace("?", "");
            sMXDName = sMXDName.Replace("!", "");
            sMXDName = sMXDName.Replace("@", "");
            sMXDName = sMXDName.Replace("#", "");
            sMXDName = sMXDName.Replace("$", "");
            sMXDName = sMXDName.Replace("%", "");
            sMXDName = sMXDName.Replace("^", "");
            sMXDName = sMXDName.Replace("*", "");
            sMXDName = sMXDName.Replace("(", "");
            sMXDName = sMXDName.Replace(")", "");
            sMXDName = sMXDName.Replace("'", "");

            return sMXDName;
        }
        #endregion


    }
}
