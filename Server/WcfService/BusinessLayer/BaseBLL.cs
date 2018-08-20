﻿using System;
using System.Linq;
using System.Text;
using System.Reflection;
using NLog;

using LSOmni.Common.Util;
using LSRetail.Omni.Domain.DataModel.Base.Retail;

namespace LSOmni.BLL
{
    public abstract class BaseBLL
    {
        private static Assembly dalAssembly = null;
        private static Assembly boAssembly = null;

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public virtual string DeviceId { get; set; }    //DeviceId is used everywhere...

        public BaseBLL()
        {
        }

        #region protected

        protected T GetDbRepository<T>()
        {
            try
            {
                //TODO read from app.config
                if (dalAssembly == null)
                {
                    string asm = GetDalAssemblyName();

                    string appPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);
                    appPath = appPath.Replace("file:\\", "");
                    asm = appPath + "\\" + asm;
                    dalAssembly = Assembly.LoadFrom(asm);
                }

                //OK to use type to create instance when only one of that type is in the assembly
                Type myType = dalAssembly.GetTypes().Where(typeof(T).IsAssignableFrom).FirstOrDefault();
                T instance = (T)dalAssembly.CreateInstance(myType.FullName, true);

                string cls = myType.FullName;
                if (instance == null)
                    throw new ApplicationException("dalAssembly.CreateInstance() failed for settings: " + cls);

                return instance;
            }
            catch (ReflectionTypeLoadException ex)
            {
                //catch those failed to load some dll..
                StringBuilder sb = new StringBuilder();
                foreach (Exception exSub in ex.LoaderExceptions)
                {
                    sb.AppendLine(exSub.Message);
                    System.IO.FileNotFoundException exFileNotFound = exSub as System.IO.FileNotFoundException;
                    if (exFileNotFound != null)
                    {
                        if (string.IsNullOrEmpty(exFileNotFound.FusionLog) == false)
                        {
                            sb.AppendLine("Fusion Log:");
                            sb.AppendLine(exFileNotFound.FusionLog);
                        }
                    }
                    sb.AppendLine();
                }
                throw new ApplicationException(sb.ToString(), ex);
            }
        }

        private string GetDalAssemblyName()
        {
            string key = "Infrastructure.Dal.AssemblyName"; //key in app.settings
            if (ConfigSetting.KeyExists(key))
                return ConfigSetting.GetString(key);
            else
                return "LSOmni.DataAccess.Dal.dll"; //just in case the key is missing in app.settings file
        }

        protected T GetBORepository<T>()
        {
            try
            {
                //TODO read from app.config
                if (boAssembly == null)
                {
                    string asm = GetBOAssemblyName();

                    string appPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);
                    appPath = appPath.Replace("file:\\", "");
                    asm = appPath + "\\" + asm;
                    boAssembly = Assembly.LoadFrom(asm);
                }

                //OK to use type to create instance when only one of that type is in the assembly
                Type myType = boAssembly.GetTypes().Where(typeof(T).IsAssignableFrom).FirstOrDefault();
                T instance = (T)boAssembly.CreateInstance(myType.FullName, true);

                string cls = myType.FullName;

                if (instance == null)
                    throw new ApplicationException("boAssembly.CreateInstance() failed for settings: " + cls);

                return instance;
            }
            catch (ReflectionTypeLoadException ex)
            {
                //catch those failed to load some dll..
                StringBuilder sb = new StringBuilder();
                foreach (Exception exSub in ex.LoaderExceptions)
                {
                    sb.AppendLine(exSub.Message);
                    System.IO.FileNotFoundException exFileNotFound = exSub as System.IO.FileNotFoundException;
                    if (exFileNotFound != null)
                    {
                        if (string.IsNullOrEmpty(exFileNotFound.FusionLog) == false)
                        {
                            sb.AppendLine("Fusion Log:");
                            sb.AppendLine(exFileNotFound.FusionLog);
                        }
                    }
                    sb.AppendLine();
                }
                throw new ApplicationException(sb.ToString(), ex);
            }
        }

        private string GetBOAssemblyName()
        {
            string key = "BOConnection.AssemblyName"; //key in app.settings
            if (ConfigSetting.KeyExists(key))
                return ConfigSetting.GetString(key);
            else
                return "LSOmni.DataAccess.BOConnection.NavSQL.dll"; //just in case the key is missing in app.settings file
        }

        protected string Base64GetFromByte(byte[] image, ImageSize imageSize)
        {
            try
            {
                int height = imageSize.Height;
                int width = imageSize.Width;
                System.Drawing.Imaging.ImageFormat imgFormat = System.Drawing.Imaging.ImageFormat.Jpeg; //default everything to Png
                return ImageConverter.BytesToBase64(image, width, height, imgFormat);
            }
            catch (Exception ex)
            {
                //HandleExceptions(ex, "Failed to GetImageFromByte() ");  //dont throw an error but return something
                string msg = "Base64GetFromByte() failed but returning empty base64 string";
                if (image == null)
                    msg += " image bytes are null";
                else
                    msg += " image bytes length: " + image.Length;

                logger.Log(LogLevel.Warn, ex, msg);
                return "";// GetImageFile("na.jpg");
            }
        }

        #endregion protected
    }
}

 