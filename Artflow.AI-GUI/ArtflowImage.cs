using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Artflow.AI_GUI
{
    public class ArtflowImage : INotifyPropertyChanged
    {
        
        private enum STATUS
        {
            INITIALIZED=1,
            QUEUED=5,
            FAIL_INAPPROPRIATE=10,
            FINISHED=15,
            FAIL_CONNECTION_PROBLEMS=20,
            FAIL_IRREPARABLE_MISC=25, // For everything unexplained 
        }

        private STATUS status = STATUS.INITIALIZED;

        private const int WAITTIME_BETWEEN_UPDATES = 60;

        private bool hidden = false;
        private int? artflowId;
        private string textPrompt;
        private string userId;
        private int? queuePosition = int.MaxValue;
        private byte[] rawImageData = new byte[0];
        private ImageSource asImageSource = null;
        private bool isFailedInappropriate = false;

        JsonSerializerOptions opt = new JsonSerializerOptions() { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString};

        private string generateUserId()
        {
            return Guid.NewGuid().ToString().Substring(4, 19); // It's a kind of a shorter version, eh
        }



        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int? ArtflowId
        {
            get { return artflowId; }
            set
            {
                artflowId = value;
                RaisePropertyChanged("Id");
            }
        }
        [Indexed]
        public string TextPrompt 
        {
            get { return textPrompt; }
            set
            {
                textPrompt = value;
                RaisePropertyChanged("TextPrompt");
            }
        }
        public string UserId 
        {
            get { return userId; }
            set
            {
                userId = value;
                RaisePropertyChanged("UserId");
            }
        }
        [Indexed]
        public int? QueuePosition 
        {
            get { return queuePosition; }
            set
            {
                queuePosition = value;
                RaisePropertyChanged("QueuePosition");
            }
        }
        public byte[] RawImageData 
        {
            get { return rawImageData; }
            set
            {
                rawImageData = value;
                RaisePropertyChanged("RawImageData");
            }
        }
        [Ignore]
        public ImageSource AsImageSource 
        {
            get { return asImageSource; }
            set
            {
                asImageSource = value;
                RaisePropertyChanged("AsImageSource");
            }
        }
        [Indexed]
        public bool IsFailedInappropriate
        {
            get { return isFailedInappropriate; }
            set
            {
                isFailedInappropriate = value;
                RaisePropertyChanged("IsFailedInappropriate");
            }
        }
        [Indexed]
        public bool Hidden
        {
            get { return hidden; }
            set
            {
                hidden = value;
                RaisePropertyChanged("Hidden");
            }
        }

        public ArtflowImage(string textPromptA = "")
        {
            textPrompt = textPromptA;
            userId = generateUserId();
            status = STATUS.INITIALIZED;

            _ = Task.Run(DoSomething);
        }

        // Don't use this to create, it's just for querying from a database.
        public ArtflowImage() 
        {

            
        }
        // Use this after reading from DB
        public void Activate(int delay=0)
        {

            _ = Task.Run(()=> {
                System.Threading.Thread.Sleep(delay*1000);
                DoSomething();
            });
        }

        // Periodically called to do some processing, the exact processing depending on the status of the image.
        private async void DoSomething()
        {

            
            

            bool irreparableFail = false;
            while (!irreparableFail)
            {

                bool temporaryFail = false;

                // Step 1
                if(artflowId == null)
                {
                    if (isFailedInappropriate)
                    {
                        irreparableFail = true;
                        break;
                    }

                    PatientRequester.Response response= await PatientRequester.post("https://artflow.ai/add_to_generation_queue", new Dictionary<string, string>()
                    {
                        { "text_prompt", textPrompt},
                        { "user_id_val", userId} 
                    });

                    if (response.success == false)
                    {
                        temporaryFail = true;
                    } else
                    {

                        Logger.Log("add_to_generation_queue",response.responseAsString);

                        try {
                            AddToGenerationQueueResponse jsonResponse = JsonSerializer.Deserialize<AddToGenerationQueueResponse>(response.responseAsString, opt);

                            if (jsonResponse.is_bad_prompt.ToLower() == "true" && jsonResponse.index == null)
                            {
                                irreparableFail = true;
                                IsFailedInappropriate = true;
                                break;
                            } else
                            {
                                IsFailedInappropriate = false;
                                ArtflowId = jsonResponse.index;
                                QueuePosition = jsonResponse.queue_length;
                            }
                        }
                        catch (Exception e){
                            temporaryFail = true;
                        }
                    }

                } else if (queuePosition.HasValue && queuePosition > -1 && artflowId.HasValue)
                {
                    // Just update the position then
                    PatientRequester.Response response = await PatientRequester.post("https://artflow.ai/check_status", new Dictionary<string, string>()
                    {
                        { "my_work_id", artflowId.Value.ToString()}
                    });

                    if (response.success == false)
                    {
                        temporaryFail = true;
                    }
                    else
                    {

                        Logger.Log("check_status-"+artflowId, response.responseAsString);

                        try
                        {
                            CheckStatusResponse jsonResponse = JsonSerializer.Deserialize<CheckStatusResponse>(response.responseAsString, opt);
                            
                            
                            if (jsonResponse.current_rank > -1)
                            {
                                // Just update position.
                                QueuePosition = jsonResponse.current_rank;
                                
                            } else if (jsonResponse.current_rank == -1)
                            {
                                // Fetch the finished image.
                                PatientRequester.Response imageFetchResponse = await PatientRequester.get("https://artflowbucket.s3.amazonaws.com/generated/"+artflowId+".png");
                                if(imageFetchResponse.success == false)
                                {
                                    temporaryFail = true;
                                } else
                                {
                                    RawImageData = imageFetchResponse.rawData;
                                    QueuePosition = -1;
                                    Application.Current.Dispatcher.Invoke(() => {
                                        Directory.CreateDirectory("images");
                                        string filename = GetUnusedFilename("images/"+artflowId+" "+textPrompt+".png");
                                        File.WriteAllBytes(filename,imageFetchResponse.rawData);


                                    });
                                }
                            }
                            else
                            {
                                // Hmmm. No idea bro!
                            }
                        }
                        catch (Exception e)
                        {
                            temporaryFail = true;
                        }
                    }
                }

                if(rawImageData != null && asImageSource == null)
                {
                    Application.Current.Dispatcher.Invoke(() => {
                        ImageSource testConvert = RawImageToImageSource(rawImageData);
                        if(testConvert != null)
                        {
                            AsImageSource = testConvert;
                        }
                    });
                }


                System.Threading.Thread.Sleep(WAITTIME_BETWEEN_UPDATES * 1000);
            }
            
        }


        /// PropertyChanged event handler
        public event PropertyChangedEventHandler PropertyChanged;

        /// Property changed Notification        
        public void RaisePropertyChanged(string propertyName)
        {
            // take a copy to prevent thread issues
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /*static ArtflowImage? CreateArtflowAIImage(string textPrompt){

        }*/

        public static string GetUnusedFilename(string baseFilename)
        {
            if (!File.Exists(baseFilename))
            {
                return baseFilename;
            }
            string extension = Path.GetExtension(baseFilename);

            int index = 1;
            while (File.Exists(Path.ChangeExtension(baseFilename, "." + (++index) + extension))) ;

            return Path.ChangeExtension(baseFilename, "." + (index) + extension);
        }

        static public BitmapImage RawImageToImageSource(byte[] rawImageData)
        {
            try
            {
                using (MemoryStream memory = new MemoryStream(rawImageData))
                {
                    memory.Position = 0;
                    BitmapImage bitmapimage = new BitmapImage();
                    bitmapimage.BeginInit();
                    bitmapimage.StreamSource = memory;
                    bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapimage.EndInit();

                    return bitmapimage;
                }
            } catch(Exception e)
            {
                return null;
            }
            
        }

    }
}
