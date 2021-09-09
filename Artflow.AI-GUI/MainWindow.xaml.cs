﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using SQLite;
using System.Collections.Specialized;
using Microsoft.Win32;

namespace Artflow.AI_GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        FullyObservableCollection<ArtflowImage> images = new FullyObservableCollection<ArtflowImage>();

        public FullyObservableCollection<ArtflowImage> Images {
            get {
                return images;
            }
        }

        private const string DATABASE_PATH = "database.db";
        private const int DELAYONLOADPERIMAGE = 5;
        public MainWindow()
        {
            this.DataContext = this;
            InitializeComponent();

            SQLiteConnection database = new SQLiteConnection(DATABASE_PATH);
            database.CreateTable<ArtflowImage>();

            TableQuery<ArtflowImage> query = database.Table<ArtflowImage>().Where(v => v.Hidden.Equals(false));

            int index = 0;
            foreach (ArtflowImage image in query)
            {
                images.Add(image);
                image.Activate((index++)* DELAYONLOADPERIMAGE);
            }

            database.Close();
            database.Dispose();

            images.CollectionChanged += Images_CollectionChanged;
            images.ItemPropertyChanged += Images_ItemPropertyChanged;
            

            //images.Add(new ArtflowImage("test 1"));
        }

        private void Images_ItemPropertyChanged(object sender, ItemPropertyChangedEventArgs<ArtflowImage> e)
        {
            lock (DATABASE_PATH)
            {
                SQLiteConnection database = new SQLiteConnection(DATABASE_PATH);
                database.CreateTable<ArtflowImage>();

                if (database.Update(e.Item) == 0) // Means it didn't affect anything
                {
                    database.Insert(e.Item);
                }

                database.Close();
                database.Dispose();
            }
        }

        private void Images_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            lock (DATABASE_PATH)
            {
                SQLiteConnection database = new SQLiteConnection(DATABASE_PATH);
                database.CreateTable<ArtflowImage>();

                if (e.Action == NotifyCollectionChangedAction.Remove ||
                e.Action == NotifyCollectionChangedAction.Replace)
                {
                    foreach (ArtflowImage item in e.OldItems)
                    {
                        item.Hidden = true;
                        if (database.Update(item) == 0) // Means it didn't affect anything
                        {
                            database.Insert(item);
                        }
                    }

                }

                if (e.Action == NotifyCollectionChangedAction.Add ||
                    e.Action == NotifyCollectionChangedAction.Replace)
                {
                    foreach (ArtflowImage item in e.NewItems)
                    {
                        if (database.Update(item) == 0) // Means it didn't affect anything
                        {
                            database.Insert(item);
                        }
                    }
                }

                database.Close();
                database.Dispose();
            }
        }


        private void AddToQueue_btn_Click(object sender, RoutedEventArgs e)
        {
            string textPromptString = textPrompt_txt.Text.Trim();
            if(textPromptString.Length > 0)
            {

                images.Add(new ArtflowImage(textPromptString));
            } else
            {
                MessageBox.Show("Well ... please enter a text prompt before submitting.");
            }
        }

        private void savePreview_btn_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory("previews");

            ArtflowImage currentImage = muhElement.DataContext as ArtflowImage;
            if(currentImage == null || currentImage.ArtflowId==null)
            {
                return;
            }
            string filename = "previews/"+ currentImage.ArtflowId + " " + currentImage.TextPrompt+".png";
            filename = Helpers.GetUnusedFilename(filename);

            int height = (int)NicePreview.ActualHeight+20;
            int width = (int)NicePreview.ActualWidth;

            RenderTargetBitmap bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);

            bmp.Render(NicePreview);

            BitmapEncoder encoder = new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(bmp));

            using (Stream stm = File.Create(filename))
            {
                encoder.Save(stm);
            }
        }
    }
}
