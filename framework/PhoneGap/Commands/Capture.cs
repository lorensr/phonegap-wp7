﻿using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Tasks;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.IO;
using System.Runtime.Serialization;
using Microsoft.Xna.Framework.Media;
using Microsoft.Phone;
using System.Windows.Media.Imaging;

namespace WP7GapClassLib.PhoneGap.Commands
{
/// <summary>
    /// Provides access to the audio, image, and video capture capabilities of the device
    /// </summary>
    public class Capture : BaseCommand
    {
        #region Internal classes (options and resultant objects)

        /// <summary>
        /// Represents captureImage action options.
        /// </summary>
        [DataContract]
        public class CaptureImageOptions
        {
            /// <summary>
            /// The maximum number of images the device user can capture in a single capture operation. The value must be greater than or equal to 1 (defaults to 1).
            /// </summary>
            [DataMember(IsRequired = false, Name="limit")]
            public int Limit { get; set; }

            public static CaptureImageOptions Default
            {
                get { return new CaptureImageOptions() { Limit = 1 }; }
            }
        }

        /// <summary>
        /// Represents getFormatData action options.
        /// </summary>
        [DataContract]
        public class MediaFormatOptions
        {
            /// <summary>
            /// The maximum number of images the device user can capture in a single capture operation. The value must be greater than or equal to 1 (defaults to 1).
            /// </summary>
            [DataMember(IsRequired=true,Name="fullPath")]
            public string FullPath { get; set; }

            [DataMember(Name="type")]
            public string Type { get; set; }
            
        }

        /// <summary>
        /// Stores image info
        /// </summary>
        [DataContract]
        public class MediaFile
        {

            [DataMember(Name = "fileName")]
            public string FileName { get; set; }

            [DataMember(Name = "filePath")]
            public string FilePath { get; set; }

            [DataMember(Name = "type")]
            public string Type { get; set; }

            [DataMember(Name = "lastModifiedDate")]
            public string LastModifiedDate { get; set; }

            [DataMember(Name = "size")]
            public long Size { get; set; }

            public MediaFile(string filePath, Picture image)
            {
                this.FilePath = filePath;
                this.FileName = System.IO.Path.GetFileName(this.FilePath);
                this.Type = MimeTypeMapper.GetMimeType(FileName);
                this.Size = image.GetImage().Length;
                this.LastModifiedDate = image.Date.ToString();

            }
        }

        /// <summary>
        /// Stores additional media file data
        /// </summary>
        [DataContract]
        public class MediaFileData
        {
            [DataMember(Name = "height")]
            public int Height { get; set; }

            [DataMember(Name = "width")]
            public int Width { get; set; }

            [DataMember(Name = "bitrate")]
            public int Bitrate { get; set; }

            [DataMember(Name = "duration")]
            public int Duration { get; set; }

            [DataMember(Name = "codecs")]
            public string Codecs { get; set; }

            public MediaFileData(WriteableBitmap image)
            {
                this.Height = image.PixelHeight;
                this.Width = image.PixelWidth;
                this.Bitrate = 0;
                this.Duration = 0;
                this.Codecs = "";
            }
        }

        #endregion

        /// <summary>
        /// Folder to store captured images
        /// </summary>
        private string isoFolder = "CapturedImagesCache";

        /// <summary>
        /// Capture Image options
        /// </summary>
        protected CaptureImageOptions captureImageOptions;

        /// <summary>
        /// Used to open camera application
        /// </summary>
        private CameraCaptureTask cameraTask;

        /// <summary>
        /// Stores informaton about captured files
        /// </summary>
        List<MediaFile> files = new List<MediaFile>();
        
        /// <summary>
        /// Launches default camera application to capture image
        /// </summary>
        /// <param name="options">may contains limit or mode parameters</param>
        public void captureImage(string options)
        {
            try
            {
                try
                {
                    this.captureImageOptions = String.IsNullOrEmpty(options) ?
                        CaptureImageOptions.Default : JSON.JsonHelper.Deserialize<CaptureImageOptions>(options);

                }
                catch (Exception ex)
                {
                    this.DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION, ex.Message));
                    return;
                }

    
                cameraTask = new CameraCaptureTask();
                cameraTask.Completed += this.cameraTask_Completed;
                cameraTask.Show();
            }
            catch (Exception e)
            {
                DispatchCommandResult(new PluginResult(PluginResult.Status.ERROR, e.Message));
            }
        }

        /// <summary>
        /// Retrieves the format information of the media file.
        /// </summary>
        /// <param name="options"></param>
        public void getFormatData(string options)
        {
            if(String.IsNullOrEmpty(options)){
                this.DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION));
                return;
            }
            
            try
            {                                
                MediaFormatOptions mediaFormatOptions;
                try
                {
                   mediaFormatOptions = JSON.JsonHelper.Deserialize<MediaFormatOptions>(options);
                }
                catch (Exception ex)
                {
                    this.DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION, ex.Message));
                    return;
                }

                if (string.IsNullOrEmpty(mediaFormatOptions.FullPath))
                {
                    DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION));
                }

                string mimeType = mediaFormatOptions.Type;

                if (string.IsNullOrEmpty(mimeType))
                {
                    mimeType = MimeTypeMapper.GetMimeType(mediaFormatOptions.FullPath);
                }

                if (mimeType.Equals("image/jpeg"))
                {
                    WriteableBitmap image = ExtractImageFromLocalStorage(mediaFormatOptions.FullPath);

                    if (image == null)
                    {
                        DispatchCommandResult(new PluginResult(PluginResult.Status.ERROR, "File not found"));
                        return;
                    }

                    MediaFileData mediaData = new MediaFileData(image);
                    DispatchCommandResult(new PluginResult(PluginResult.Status.OK, mediaData));
                }
                else
                {
                    DispatchCommandResult(new PluginResult(PluginResult.Status.ERROR));
                }
            }
            catch (Exception e)
            {
                DispatchCommandResult(new PluginResult(PluginResult.Status.ERROR));
            }
        }

        /// <summary>
        /// Handles result of capture to save image information 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">stores inforamation about currrent captured image</param>
        private void cameraTask_Completed(object sender, PhotoResult e)
        {
            
            if (e.Error != null)
            {
                DispatchCommandResult(new PluginResult(PluginResult.Status.ERROR));
                return;
            }

            switch (e.TaskResult)
            {
                case TaskResult.OK:
                    try
                    {
                        string fileName = System.IO.Path.GetFileName(e.OriginalFileName);

                        // Save image in media library
                        MediaLibrary library = new MediaLibrary();
                        Picture image = library.SavePicture(fileName, e.ChosenPhoto);

                        // Save image in isolated storage    
                    
                        // we should return stream position back after saving stream to media library
                        e.ChosenPhoto.Seek(0, SeekOrigin.Begin);
                        byte[] imageBytes = new byte[e.ChosenPhoto.Length];
                        e.ChosenPhoto.Read(imageBytes, 0, imageBytes.Length);
                        string pathLocalStorage = this.SaveImageToLocalStorage(fileName, isoFolder, imageBytes);                                              
                        
                        // Get image data
                        MediaFile data = new MediaFile(pathLocalStorage, image);

                        this.files.Add(data);

                        if (files.Count < this.captureImageOptions.Limit)
                        {
                            cameraTask.Show();
                        }
                        else
                        {
                            DispatchCommandResult(new PluginResult(PluginResult.Status.OK, files, "navigator.device.capture._castMediaFile"));
                            files.Clear();
                        }
                    }
                    catch(Exception ex) 
                    {
                        DispatchCommandResult(new PluginResult(PluginResult.Status.ERROR,"Error capturing image."));
                    }
                    break;

                case TaskResult.Cancel:
                    if (files.Count > 0)
                    {
                        // User canceled operation, but some images were made
                        DispatchCommandResult(new PluginResult(PluginResult.Status.OK, files, "navigator.device.capture._castMediaFile"));
                        files.Clear();
                    }
                    else
                    {
                        DispatchCommandResult(new PluginResult(PluginResult.Status.ERROR, "Canceled."));
                    }
                    break;
            
                default:
                    if (files.Count > 0)
                    {
                        DispatchCommandResult(new PluginResult(PluginResult.Status.OK, files, "navigator.device.capture._castMediaFile"));
                        files.Clear();
                    }
                    else
                    {
                        DispatchCommandResult(new PluginResult(PluginResult.Status.ERROR, "Did not complete!"));
                    }
                    break;               
            }         
        }

        /// <summary>
        /// Extract file from Isolated Storage as WriteableBitmap object
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private WriteableBitmap ExtractImageFromLocalStorage(string filePath)
        {
            try
            {

                var isoFile = IsolatedStorageFile.GetUserStoreForApplication();

                using (var imageStream = isoFile.OpenFile(filePath, FileMode.Open, FileAccess.Read))
                {
                    var imageSource = PictureDecoder.DecodeJpeg(imageStream);
                    return imageSource;
                }
            }catch(Exception e){
                return null;
            }
        }


        /// <summary>
        /// Saves captured image in isolated storage
        /// </summary>
        /// <param name="imageFileName">image file name</param>
        /// <param name="imageFolder">folder to store images</param>
        /// <returns>Image path</returns>
        private string SaveImageToLocalStorage(string imageFileName, string imageFolder, byte[] imageBytes)
        {
            if (imageBytes == null)
            {
                throw new ArgumentNullException("imageBytes");
            }
            try
            {
                var isoFile = IsolatedStorageFile.GetUserStoreForApplication();     
                
                if (!isoFile.DirectoryExists(imageFolder))
                {
                    isoFile.CreateDirectory(imageFolder);
                }
                string filePath = System.IO.Path.Combine(imageFolder, imageFileName);

                using (var stream = isoFile.CreateFile(filePath))
                {
                    stream.Write(imageBytes, 0, imageBytes.Length);
                }

                return filePath;
            }
            catch(Exception e)
            {
                //TODO: log or do something else
                throw;
            }
        }  
        

    }
}
