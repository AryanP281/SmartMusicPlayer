using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Resources;
using TagLib;
using GUI.Files;
using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GUI
{
    public partial class Form1 : Form
    {
        const string DLL_ADDR = @"C:\Users\Aryan\Documents\Visual Studio 2017\Projects\SmartMusicPlayer\Debug\SmartMusicPlayer.dll";

        [DllImport(DLL_ADDR, CallingConvention = CallingConvention.Cdecl)]
        static extern void Initialize(StringBuilder exePath, ulong numOfNewSongs);
        [DllImport(DLL_ADDR, CallingConvention = CallingConvention.Cdecl)]
        static extern void Train(ulong songIndex);
        [DllImport(DLL_ADDR, CallingConvention = CallingConvention.Cdecl)]
        static extern void GetFeedback(int songDuration, int durationHeard);
        [DllImport(DLL_ADDR, CallingConvention = CallingConvention.Cdecl)]
        static extern ulong Output();
        [DllImport(DLL_ADDR, CallingConvention = CallingConvention.Cdecl)]
        static extern void BeforeClosing();

        readonly string INI_FILE_ADDR;
        readonly Size defaultAspectRatio; //The aspect ratio assumed during development
        string MUSIC_FOLDER; //The address of the folder in which the music files are located

        DatabaseManager dbManager = new DatabaseManager(); /*Handles the song
        database*/

        enum Windows
        {
            ALL_SONG_LIST, ARTIST_SONG_LIST, ALBUM_SONG_LIST, GENRES_SONG_LIST,
            SEARCH_RESULTS, SONG_PLAYING
        }; //Identifiers for the different windows in the application
        Windows currentWindow; //The currently active window of the application
        Windows lastWindow; //The last active window of the application

        //The primary font
        string primaryFontName = "Quartz MS";

        List<SongCard> songCards = new List<SongCard>(); //A list of the song cards
        int numOfCardsOnScreen = 0; /*The number of song cards displayed 
        on the screen*/
        List<KeyValuePair<Label, List<SongCard>>> classifiedCards = new List<KeyValuePair<Label, List<SongCard>>>();
        /*A list of the classification labels and their respective song cards*/
        const int CARD_START_INDEX = 7; /*The index after which the cards are located
        in the controls list*/

        int mouseScrollCounter = 0; /*A counter which keeps track of the mouse scroll
        values*/

        bool songPlaying = false; //Tells whether a song is currently playing

        WaveOutEvent outputDevice; //The output device for playing the audio
        AudioFileReader audioFile; //The audio file to play

        float userVolume = 1.0f; //The volume set by the user

        Thread parallelThread; //A thread for parallel operations

        public Form1()
        {
            //Generating the file address
            INI_FILE_ADDR = Directory.GetCurrentDirectory() + "//SmartPlayer.ini";

            //Initializing the default aspect ratio
            defaultAspectRatio = new Size(1280, 720);

            //Setting the mouse scroll event
            this.MouseWheel += new MouseEventHandler(OnMouseScroll);

            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Size = defaultAspectRatio;

            Initialize(); //Initializes the app

            Application.ApplicationExit += new EventHandler(OnApplicationExit);
        }

        private void Initialize()
        {
            //Reading Initialization data
            ReadInitializationData();

            //Initializing the database
            dbManager.OpenDatabase();

            //Loading songs
            dbManager.LoadSongs(MUSIC_FOLDER);

            //Initializing the bot
            StringBuilder path = new StringBuilder(Directory.GetCurrentDirectory());
            Initialize(path, (ulong)dbManager.numOfNewSongs);

            //Initializing the current state of the application window
            lastWindow = Windows.ALL_SONG_LIST;
            currentWindow = Windows.ALL_SONG_LIST;
            SwitchWindow(currentWindow); //Drawing the current window
        }

        private void ReadInitializationData()
        {
            try
            {
                if (System.IO.File.Exists(INI_FILE_ADDR)) //Checking if the .ini file exists
                {
                    //Getting the address of the music folder
                    StreamReader sr = new StreamReader(INI_FILE_ADDR);
                    MUSIC_FOLDER = sr.ReadLine();
                    userVolume = (float)Convert.ToDouble(sr.ReadLine());
                    sr.Close();
                }
                else //Creating the file
                {
                    //Getting the address of the music folder
                    GetMusicFolder();

                    TextWriter newFile = new StreamWriter(INI_FILE_ADDR);
                    newFile.WriteLine(MUSIC_FOLDER);
                    newFile.WriteLine(userVolume);
                    newFile.Close();

                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to read initialization file ! \n" + e.Message);
            }
        }

        void GetMusicFolder()
        {
            FolderBrowserDialog fd = new FolderBrowserDialog(); //The file dialog
            fd.Description = "Find Music Folder";

            if (fd.ShowDialog() == DialogResult.OK)
            {
                MUSIC_FOLDER = fd.SelectedPath;
            }
        }

        private void SwitchWindow(Windows newWindow)
        {
            lastWindow = currentWindow;
            currentWindow = newWindow;
            mouseScrollCounter = 0;

            switch (currentWindow)
            {
                case Windows.ALL_SONG_LIST: DrawAllSongsListWindow(); break;
                case Windows.ARTIST_SONG_LIST: DrawArtistSongsListWindow(); break;
                case Windows.ALBUM_SONG_LIST: DrawAlbumSongsListWindow(); break;
                case Windows.GENRES_SONG_LIST: DrawGenresSongsListWindow(); break;
                default: throw new Exception("Invalid current window");
            }
        }

        private List<int> SortSongs(DataSet data)
        {
            List<KeyValuePair<string, int>> unsortedSongs = new List<KeyValuePair<string, int>>();
            List<int> sortedSongs = new List<int>();

            //Getting the songs
            for (int a = 0; a < data.Tables[0].Rows.Count; ++a)
            {
                KeyValuePair<string, int> song =
                    new KeyValuePair<string, int>(data.Tables[0].Rows[a]["Title"].ToString(), a);

                unsortedSongs.Add(song);
            }

            //Sorting the songs
            unsortedSongs = unsortedSongs.OrderBy(kvp => kvp.Key).ToList();
            foreach (KeyValuePair<string, int> song in unsortedSongs)
            {
                sortedSongs.Add(song.Value);
            }

            return sortedSongs;
        }

        private void AddSongCardToControlsList(SongCard sc)
        {
            sc.RepositionControls();
            this.Controls.Add(sc.AlbumArt);
            this.Controls.Add(sc.SongNameLabel);
            this.Controls.Add(sc.ArtistNameLabel);
            this.Controls.Add(sc.AlbumNameLabel);
            this.Controls.Add(sc.CardButton);
        }

        private List<KeyValuePair<string, List<int>>> ReturnArtistsAndTheirSongs(ref DataSet data)
        {
            List<KeyValuePair<string, List<int>>> artistsAndSongs = new List<KeyValuePair<string, List<int>>>();

            //Compiling the list of artists
            for (int a = 0; a < data.Tables[0].Rows.Count; ++a)
            {
                if (Convert.ToInt32(data.Tables[0].Rows[a]["Checked"]) == 1)
                {
                    //Getting the artist
                    string artistName = data.Tables[0].Rows[a]["PrimaryArtist"].ToString();

                    //Checking if the artist already exists in the list
                    int searchResult = CheckIfKeyExistsInList<string, List<int>>(artistName, artistsAndSongs);
                    if (searchResult < 0)
                    {
                        KeyValuePair<string, List<int>> artist = new KeyValuePair<string, List<int>>(artistName, new List<int>());
                        artist.Value.Add(Convert.ToInt32(data.Tables[0].Rows[a]["Id"]));
                        artistsAndSongs.Add(artist);
                    }
                    else
                        artistsAndSongs[searchResult].Value.Add(Convert.ToInt32(data.Tables[0].Rows[a]["Id"]));

                }
            }

            artistsAndSongs = artistsAndSongs.OrderBy(kvp => kvp.Key).ToList();
            return artistsAndSongs;
        }

        private List<KeyValuePair<string, List<int>>> ReturnAlbumsAndSongs(ref DataSet data)
        {
            List<KeyValuePair<string, List<int>>> albumsAndSongs = new List<KeyValuePair<string, List<int>>>();

            for (int a = 0; a < data.Tables[0].Rows.Count; ++a)
            {
                DataRow row = data.Tables[0].Rows[a];

                if (Convert.ToInt32(row["Checked"]) == 1)
                {
                    string albumName = "Unknown";
                    if (row["Format"].ToString() == "MP3 ")
                    {
                        TagLib.File tgFile = TagLib.File.Create(dbManager.ReturnFilePath(Convert.ToInt32(row["Id"])));
                        if (tgFile != null)
                        {
                            if (tgFile.Tag.Album != null)
                                albumName = tgFile.Tag.Album;
                        }
                    }

                    //Checking if album already exists in list
                    int searchResult = CheckIfKeyExistsInList<string, List<int>>(albumName, albumsAndSongs);
                    if (searchResult < 0)
                    {
                        KeyValuePair<string, List<int>> album = new KeyValuePair<string, List<int>>(albumName, new List<int>());
                        album.Value.Add(Convert.ToInt32(row["Id"]));
                        albumsAndSongs.Add(album);
                    }
                    else
                        albumsAndSongs[searchResult].Value.Add(Convert.ToInt32(row["Id"]));
                }
            }

            albumsAndSongs = albumsAndSongs.OrderBy(kvp => kvp.Key).ToList();
            return albumsAndSongs;
        }

        private List<KeyValuePair<string, List<int>>> ReturnGenresAndSongs(ref DataSet data)
        {
            List<KeyValuePair<string, List<int>>> genresAndSongs = new List<KeyValuePair<string, List<int>>>();

            for (int a = 0; a < data.Tables[0].Rows.Count; ++a)
            {
                DataRow row = data.Tables[0].Rows[a];

                if (Convert.ToInt32(row["Checked"]) == 1)
                {
                    List<string> genreNames = new List<string>();
                    if (row["Format"].ToString() == "MP3 ")
                    {
                        TagLib.File tgFile = TagLib.File.Create(dbManager.ReturnFilePath(Convert.ToInt32(row["Id"])));
                        if (tgFile != null)
                        {
                            if (tgFile.Tag.Genres.Length != 0)
                            {
                                foreach (string genre in tgFile.Tag.Genres)
                                {
                                    genreNames.Add(genre);
                                }
                            }
                            else
                                genreNames.Add("Unknown");
                        }
                    }
                    else
                    {
                        genreNames.Add("Unknown");
                    }

                    //Checking if album already exists in list
                    foreach (string genreName in genreNames)
                    {
                        int searchResult = CheckIfKeyExistsInList<string, List<int>>(genreName, genresAndSongs);
                        if (searchResult < 0)
                        {
                            KeyValuePair<string, List<int>> genre = new KeyValuePair<string, List<int>>(genreName, new List<int>());
                            genre.Value.Add(Convert.ToInt32(row["Id"]));
                            genresAndSongs.Add(genre);
                        }
                        else
                            genresAndSongs[searchResult].Value.Add(Convert.ToInt32(row["Id"]));
                    }
                }
            }
        
            genresAndSongs = genresAndSongs.OrderBy(kvp => kvp.Key).ToList();
            return genresAndSongs;
        }

        private List<int> ReturnSearchResults(ref DataSet data, string query)
        {
            List<int> songs = new List<int>();
            query = query.ToLower();

            for(int a = 0; a < data.Tables[0].Rows.Count; ++a)
            {
                DataRow row = data.Tables[0].Rows[a];
                if(row["Format"].ToString() == "MP3 ")
                {
                    TagLib.File tgFile = TagLib.File.Create
                        (dbManager.ReturnFilePath(Convert.ToInt32(row["Id"])));

                    if(tgFile != null)
                    {
                        if(row["Title"].ToString().ToLower().Contains(query))
                        {
                            songs.Add(Convert.ToInt32(row["Id"]));
                            continue;
                        }

                        foreach(string artist in tgFile.Tag.Artists)
                        {
                            if(artist.ToLower().Contains(query))
                            {
                                songs.Add(Convert.ToInt32(row["Id"]));
                                break;
                            }
                        }
                    }
                    else if(row["Title"].ToString().ToLower().Contains(query)
                       || row["PrimaryArtist"].ToString().ToLower().Contains(query))
                    {
                        songs.Add(Convert.ToInt32(row["Id"]));
                    }
                } 
                else
                {
                    if (row["Title"].ToString().ToLower().Contains(query))
                        songs.Add(Convert.ToInt32(row["Id"]));
                }
            }

            return songs;
        }

        private int CheckIfKeyExistsInList<K, V>(K key, List<KeyValuePair<K, V>> list)
        {
            for (int a = 0; a < list.Count; ++a)
            {
                if (key.Equals(list[a].Key))
                    return a;
            }

            return -1;
        }

        private void GetSongs(ref List<KeyValuePair<string, List<int>>> data, int index, ref List<SongCard> buffer)
        {
            foreach (int id in data[index].Value)
            {
                for (int a = 0; a < songCards.Count; ++a)
                {
                    if (songCards[a].SongId == id)
                    {
                        buffer.Add(songCards[a]);
                        break;
                    }
                }
            }
        }

        private void GetSongs(ref List<int> data, ref List<SongCard> buffer)
        {
            for(int a = 0; a < data.Count; ++a)
            {
                foreach (SongCard sc in songCards)
                {
                    if((int)sc.SongId == data[a])
                    {
                        buffer.Add(sc);
                        break;
                    }
                }
            }
        }

        private SongCard GetSong(int songId)
        {
            for(int a = 0; a < songCards.Count; ++a)
            {
                if (songCards[a].SongId == songId)
                    return songCards[a];
            }

            return null;
        }

        private void PlaySong(int songId)
        {
            //Switching the windows
            lastWindow = currentWindow;
            currentWindow = Windows.SONG_PLAYING;
            DrawSongPlayingWindow(songId);
        }

        private void TrackSongPosition(object progressBar)
        {
            TrackBar pB = progressBar as TrackBar;

            if (songPlaying && pB != null)
            {
                pB.Value = (int)audioFile.CurrentTime.TotalSeconds;
            }
        }

        #region GUI Rendering

        private void DrawAllSongsListWindow()
        {
            if (lastWindow == Windows.ALL_SONG_LIST || lastWindow == Windows.SONG_PLAYING)
            {
                //Initializing
                this.Controls.Clear();

                float[] regionGoldenRatio = { 2.0f, 90.0f, 8.0f };
                AnchorStyles allAnchors = AnchorStyles.Right | AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Left;

                //Setting up the form
                this.BackColor = Color.Black;

                //Creating the searchbar
                System.Windows.Forms.TextBox searchBar = new System.Windows.Forms.TextBox();
                searchBar.Location = new Point(0, 0);
                searchBar.Size = new Size(this.Width - 100, (int)((float)this.Height * regionGoldenRatio[0] / 100.0f));
                searchBar.BackColor = Color.FromArgb(41, 41, 41);
                searchBar.Font = new Font(primaryFontName, 20, FontStyle.Bold | FontStyle.Italic);
                searchBar.ForeColor = Color.White;
                searchBar.Text = "SEARCH";
                searchBar.BorderStyle = BorderStyle.None;
                searchBar.Anchor = allAnchors;
                this.Controls.Add(searchBar);

                //Creating the search button
                PictureBox searchButton = new PictureBox();
                searchButton.Location = new Point(searchBar.Location.X + searchBar.Width + 10, 5);
                searchButton.Size = new Size(70, 20);
                searchButton.Image = Properties.Resources.SearchNonHover;
                searchButton.SizeMode = PictureBoxSizeMode.StretchImage;
                searchButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
                searchButton.MouseHover += new EventHandler(OnSearchButtonHover);
                searchButton.MouseLeave += new EventHandler(OnSearchButtonMouseLeave);
                searchButton.MouseClick += new MouseEventHandler(OnSearchButtonClicked);
                this.Controls.Add(searchButton);

                //Adding the smart player button
                PictureBox smartPlayerButton = new PictureBox();
                smartPlayerButton.Size = new Size(50,50);
                smartPlayerButton.BackColor = Color.FromArgb(255,255,255);
                smartPlayerButton.Image = Properties.Resources.Smart_Player_Symbol;
                smartPlayerButton.SizeMode = PictureBoxSizeMode.StretchImage;
                smartPlayerButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                smartPlayerButton.MouseHover += OnControlButtonMouseHover;
                smartPlayerButton.MouseLeave += OnControlButtonMouseLeave;
                this.Controls.Add(smartPlayerButton);

                //Creating the filter buttons
                Button[] filterButtons = new Button[4];
                for (byte a = 0; a < 4; ++a)
                {
                    filterButtons[a] = new Button();
                    filterButtons[a].Size = new Size(this.Width / 4,
                        (int)((float)this.Height * regionGoldenRatio[2] / 100.0f));
                    filterButtons[a].Location = new Point(a * (this.Width / 4),
                        this.Height - 100);
                    filterButtons[a].BackColor = Color.FromArgb(41, 41, 41);
                    filterButtons[a].Font = new Font(primaryFontName, 20, FontStyle.Bold | FontStyle.Italic);
                    if (a == 0)
                        filterButtons[a].ForeColor = Color.White;
                    else
                        filterButtons[a].ForeColor = Color.Black;
                    filterButtons[a].FlatStyle = FlatStyle.Popup;
                    filterButtons[a].Anchor = AnchorStyles.Bottom;
                    filterButtons[a].MouseHover += new EventHandler(OnFilterButtonHover);
                    filterButtons[a].MouseLeave += new EventHandler(OnFilterButtonMouseLeave);
                    filterButtons[a].MouseClick += new MouseEventHandler(OnFilterButtonClicked);
                    this.Controls.Add(filterButtons[a]);
                }
                filterButtons[0].Name = "all_F_B";
                filterButtons[0].Text = "ALL";
                filterButtons[1].Name = "artists_F_B";
                filterButtons[1].Text = "Artists";
                filterButtons[2].Name = "albums_F_B";
                filterButtons[2].Text = "Albums";
                filterButtons[3].Name = "genres_F_B";
                filterButtons[3].Text = "Genres";

                smartPlayerButton.Location = new Point(filterButtons[3].Location.X +
                    (filterButtons[3].Width / 2), filterButtons[3].Location.Y
                    - (2 * smartPlayerButton.Height));

                //Creating the vertical scrollbar for the song list
                VScrollBar scrollBar = new VScrollBar();
                scrollBar.Location = new Point(this.Width - (scrollBar.Width * 2), searchBar.Location.Y + searchBar.Height + 10);
                scrollBar.Height = filterButtons[0].Location.Y - scrollBar.Location.Y;
                scrollBar.Anchor = AnchorStyles.Right | AnchorStyles.Bottom | AnchorStyles.Top;
                scrollBar.Minimum = 0;
                scrollBar.ValueChanged += new EventHandler(OnMusicScrollBarScroll);
                this.Controls.Add(scrollBar);

                //Creating the song cards
                DataSet data = dbManager.ReturnAllData(); //Getting the song data
                int availableScreenSpace = Screen.PrimaryScreen.Bounds.Height - searchBar.Height - filterButtons[0].Height - 20;
                List<int> sortedSongs = SortSongs(data);

                for (int a = 0; a < sortedSongs.Count; a++)
                {
                    int databaseElementIndex = sortedSongs[a];

                    if (Convert.ToInt32(data.Tables[0].Rows[databaseElementIndex]["Checked"]) == 1)
                    {
                        SongCard sc = new SongCard(Convert.ToUInt16(data.Tables[0].Rows[databaseElementIndex]["Id"]));
                        string filePath = dbManager.ReturnFilePath(Convert.ToInt16(data.Tables[0].Rows[databaseElementIndex]["Id"]));
                        TagLib.File tgFile = null;
                        if (data.Tables[0].Rows[databaseElementIndex]["Format"].ToString() == "MP3 ")
                            tgFile = TagLib.File.Create(filePath);

                        sc.CardButton.Size = new Size(this.Width, (this.Height - searchBar.Height - filterButtons[0].Height - 20) / 8);
                        if (a == 0)
                            sc.CardButton.Location = new Point(0, searchBar.Location.Y + searchBar.Height + 10);
                        else
                            sc.CardButton.Location = new Point(0, songCards[a - 1].CardButton.Location.Y + sc.CardButton.Height);
                        sc.CardButton.BackColor = Color.FromArgb(41, 41, 41);
                        sc.CardButton.FlatStyle = FlatStyle.Popup;
                        sc.CardButton.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                        sc.CardButton.MouseClick += new MouseEventHandler(OnCardButtonClicked);

                        sc.AlbumArt.Size = new Size(sc.CardButton.Height - 10, sc.CardButton.Height - 10);
                        if (tgFile != null && tgFile.Tag.Pictures.Length >= 1)
                        {
                            MemoryStream imgStream = new MemoryStream(tgFile.Tag.Pictures[0].Data.Data);
                            sc.AlbumArt.Image = Image.FromStream(imgStream);
                        }
                        else
                            sc.AlbumArt.Image = Properties.Resources.Default_Album_Art;
                        sc.AlbumArt.SizeMode = PictureBoxSizeMode.StretchImage;
                        sc.AlbumArt.Anchor = AnchorStyles.Left | AnchorStyles.Top;

                        sc.SongNameLabel.AutoSize = true;
                        sc.SongNameLabel.Font = new Font(primaryFontName, 12, FontStyle.Bold);
                        sc.SongNameLabel.Text = data.Tables[0].Rows[databaseElementIndex]["Title"].ToString();
                        sc.SongNameLabel.ForeColor = Color.White;
                        sc.SongNameLabel.BackColor = Color.FromArgb(41, 41, 41);
                        sc.SongNameLabel.Anchor = sc.CardButton.Anchor;

                        sc.ArtistNameLabel.AutoSize = true;
                        sc.ArtistNameLabel.Font = new Font(primaryFontName, 12, FontStyle.Bold);
                        if (tgFile != null)
                        {
                            foreach (string artist in tgFile.Tag.Artists)
                            {
                                sc.ArtistNameLabel.Text += artist + "; ";
                            }
                        }
                        else
                        {
                            sc.ArtistNameLabel.Text = data.Tables[0].Rows[databaseElementIndex]["PrimaryArtist"].ToString();
                        }
                        sc.ArtistNameLabel.ForeColor = Color.White;
                        sc.ArtistNameLabel.BackColor = Color.FromArgb(41, 41, 41);
                        sc.ArtistNameLabel.Anchor = sc.CardButton.Anchor;

                        sc.AlbumNameLabel.AutoSize = true;
                        sc.AlbumNameLabel.Font = new Font(primaryFontName, 12, FontStyle.Bold);
                        if (tgFile != null)
                            sc.AlbumNameLabel.Text = tgFile.Tag.Album;
                        else
                            sc.AlbumNameLabel.Text = "";
                        sc.AlbumNameLabel.ForeColor = Color.White;
                        sc.AlbumNameLabel.BackColor = Color.FromArgb(41, 41, 41);
                        sc.ArtistNameLabel.Anchor = sc.CardButton.Anchor;

                        if (sc.CardButton.Height * a < availableScreenSpace + sc.CardButton.Height)
                        {
                            AddSongCardToControlsList(sc);
                            ++numOfCardsOnScreen;
                        }
                        songCards.Add(sc);
                    }
                    scrollBar.Maximum = songCards.Count - 1;
                }
            }
            else
            {
                //Removing the unwanted controls
                int initialCount = this.Controls.Count;
                for (int a = initialCount - 1; a > CARD_START_INDEX; --a)
                {
                    this.Controls.RemoveAt(a);
                }
                numOfCardsOnScreen = 0;

                //Resetting the scroll bar
                VScrollBar scrollBar = this.Controls[CARD_START_INDEX] as VScrollBar;
                scrollBar.Value = 0;
                scrollBar.Maximum = songCards.Count;

                //Getting the song cards
                Size cardSize = new Size(this.Width,
                     (this.Height - Controls[0].Height - Controls[2].Height - 20) / 8);
                int availableScreenSpace = Screen.PrimaryScreen.Bounds.Height -
                    Controls[0].Height - Controls[2].Height - 20;
                for (int a = 0; a < songCards.Count && cardSize.Height * numOfCardsOnScreen < availableScreenSpace; ++a, ++numOfCardsOnScreen)
                {
                    songCards[a].CardButton.Size = cardSize;
                    songCards[a].CardButton.Location = new Point(0,
                        (this.Controls[0].Location.Y + this.Controls[0].Height)
                        + (cardSize.Height * a));

                    AddSongCardToControlsList(songCards[a]);
                }
            }
        }

        private void DrawArtistSongsListWindow()
        {
            if (lastWindow != Windows.SONG_PLAYING)
            {
                //Initializing
                int initalCount = this.Controls.Count;
                for (int a = initalCount - 1; a > CARD_START_INDEX; --a)
                {
                    this.Controls.RemoveAt(a);
                }
                classifiedCards.Clear();
                numOfCardsOnScreen = 0;

                //Getting all the songs in the database
                DataSet data = dbManager.ReturnAllData();
                //Getting a sorted list of artists and songs
                List<KeyValuePair<string, List<int>>> artistsAndSongs = ReturnArtistsAndTheirSongs(ref data);

                //Getting the already existing controls
                System.Windows.Forms.TextBox searchBar = this.Controls[0] as System.Windows.Forms.TextBox;
                PictureBox searchButton = this.Controls[1] as PictureBox;
                Button[] filterButtons = new Button[4];
                for (int a = 3; a < CARD_START_INDEX; ++a)
                {
                    filterButtons[a - 3] = this.Controls[a] as Button;
                }
                VScrollBar scrollBar = this.Controls[this.Controls.Count - 1] as VScrollBar;
                scrollBar.Value = 0;
                scrollBar.Maximum = songCards.Count + artistsAndSongs.Count;

                //Creating the labels for artist names
                int availableScreenSpace = Screen.PrimaryScreen.Bounds.Height - searchBar.Height - filterButtons[0].Height;
                for (int a = 0; a < artistsAndSongs.Count; ++a)
                {
                    //Getting the song cards for the artist
                    List<SongCard> songs = new List<SongCard>();
                    GetSongs(ref artistsAndSongs, a, ref songs);

                    if (songs.Count != 0)
                    {
                        //Initializing the label
                        Label artistNameLabel = new Label();
                        artistNameLabel.Size = songs[0].CardButton.Size;
                        artistNameLabel.Font = new Font(primaryFontName, 28, FontStyle.Bold);
                        if (artistsAndSongs[a].Key == "")
                            artistNameLabel.Text = "Unknown";
                        else
                            artistNameLabel.Text = artistsAndSongs[a].Key;
                        artistNameLabel.ForeColor = Color.White;
                        classifiedCards.Add(new KeyValuePair<Label, List<SongCard>>(artistNameLabel, songs));

                        //Drawing the label and song cards on screen
                        int cardHeight = artistNameLabel.Height;
                        if ((cardHeight * numOfCardsOnScreen) + 10 <= availableScreenSpace)
                        {
                            artistNameLabel.Location = new Point(0,
                                (searchBar.Location.Y + searchBar.Height) + (cardHeight * numOfCardsOnScreen) + 10);
                            this.Controls.Add(artistNameLabel);
                            ++numOfCardsOnScreen;

                            for (int b = 0; b < songs.Count && (numOfCardsOnScreen * artistNameLabel.Height) <= availableScreenSpace; ++b, ++numOfCardsOnScreen)
                            {
                                songs[b].CardButton.Size = new Size(this.Width, artistNameLabel.Height);
                                songs[b].CardButton.Location = new Point(0, (artistNameLabel.Location.Y) + (cardHeight * (b + 1)));
                                AddSongCardToControlsList(songs[b]);
                            }
                        }
                    }
                }
            }
            else
            {

            }
        }

        private void DrawAlbumSongsListWindow()
        {
            if (lastWindow != Windows.SONG_PLAYING)
            {
                //Initializing
                int initalCount = this.Controls.Count;
                for (int a = initalCount - 1; a > CARD_START_INDEX; --a)
                {
                    this.Controls.RemoveAt(a);
                }
                classifiedCards.Clear();
                numOfCardsOnScreen = 0;

                //Getting all the songs in the database
                DataSet data = dbManager.ReturnAllData();
                //Getting a sorted list of albums and songs
                List<KeyValuePair<string, List<int>>> albumsAndSongs = ReturnAlbumsAndSongs(ref data);

                //Getting the already existing controls
                System.Windows.Forms.TextBox searchBar = this.Controls[0] as System.Windows.Forms.TextBox;
                PictureBox searchButton = this.Controls[1] as PictureBox;
                Button[] filterButtons = new Button[5];
                for (int a = 3; a < CARD_START_INDEX; ++a)
                {
                    filterButtons[a - 3] = this.Controls[a] as Button;
                }
                VScrollBar scrollBar = this.Controls[this.Controls.Count - 1] as VScrollBar;
                scrollBar.Value = 0;
                scrollBar.Maximum = songCards.Count + albumsAndSongs.Count;

                //Creating the labels for artist names
                int availableScreenSpace = Screen.PrimaryScreen.Bounds.Height - searchBar.Height - filterButtons[0].Height;
                for (int a = 0; a < albumsAndSongs.Count; ++a)
                {
                    //Getting the song cards for the artist
                    List<SongCard> songs = new List<SongCard>();
                    GetSongs(ref albumsAndSongs, a, ref songs);

                    if (songs.Count != 0)
                    {
                        //Initializing the label
                        Label albumNameLabel = new Label();
                        albumNameLabel.Size = songs[0].CardButton.Size;
                        albumNameLabel.Font = new Font(primaryFontName, 28, FontStyle.Bold);
                        albumNameLabel.Text = albumsAndSongs[a].Key;
                        albumNameLabel.ForeColor = Color.White;
                        classifiedCards.Add(new KeyValuePair<Label, List<SongCard>>(albumNameLabel, songs));

                        //Drawing the label and song cards on screen
                        int cardHeight = albumNameLabel.Height;
                        if ((cardHeight * numOfCardsOnScreen) + 10 <= availableScreenSpace)
                        {
                            albumNameLabel.Location = new Point(0,
                                (searchBar.Location.Y + searchBar.Height) + (cardHeight * numOfCardsOnScreen) + 10);
                            this.Controls.Add(albumNameLabel);
                            ++numOfCardsOnScreen;

                            for (int b = 0; b < songs.Count && (numOfCardsOnScreen * albumNameLabel.Height) <= availableScreenSpace; ++b, ++numOfCardsOnScreen)
                            {
                                songs[b].CardButton.Size = new Size(this.Width, albumNameLabel.Height);
                                songs[b].CardButton.Location = new Point(0, (albumNameLabel.Location.Y) + (cardHeight * (b + 1)));
                                AddSongCardToControlsList(songs[b]);
                            }
                        }
                    }
                }

            }
            else
            {

            }
        }

        private void DrawGenresSongsListWindow()
        {
            if (lastWindow != Windows.SONG_PLAYING)
            {
                //Initializing
                int initalCount = this.Controls.Count;
                for (int a = initalCount - 1; a > CARD_START_INDEX; --a)
                {
                    this.Controls.RemoveAt(a);
                }
                classifiedCards.Clear();
                numOfCardsOnScreen = 0;

                //Getting all the songs in the database
                DataSet data = dbManager.ReturnAllData();
                //Getting a sorted list of albums and songs
                List<KeyValuePair<string, List<int>>> genresAndSongs = ReturnGenresAndSongs(ref data);

                //Getting the already existing controls
                System.Windows.Forms.TextBox searchBar = this.Controls[0] as System.Windows.Forms.TextBox;
                PictureBox searchButton = this.Controls[1] as PictureBox;
                Button[] filterButtons = new Button[5];
                for (int a = 3; a < CARD_START_INDEX; ++a)
                {
                    filterButtons[a - 3] = this.Controls[a] as Button;
                }
                VScrollBar scrollBar = this.Controls[this.Controls.Count - 1] as VScrollBar;
                scrollBar.Value = 0;
                scrollBar.Maximum = songCards.Count + genresAndSongs.Count;

                //Creating the labels for artist names
                int availableScreenSpace = Screen.PrimaryScreen.Bounds.Height - searchBar.Height - filterButtons[0].Height;
                for (int a = 0; a < genresAndSongs.Count; ++a)
                {
                    //Getting the song cards for the artist
                    List<SongCard> songs = new List<SongCard>();
                    GetSongs(ref genresAndSongs, a, ref songs);

                    if (songs.Count != 0)
                    {
                        //Initializing the label
                        Label genreNameLabel = new Label();
                        genreNameLabel.Size = songs[0].CardButton.Size;
                        genreNameLabel.Font = new Font(primaryFontName, 28, FontStyle.Bold);
                        genreNameLabel.Text = genresAndSongs[a].Key;
                        genreNameLabel.ForeColor = Color.White;
                        classifiedCards.Add(new KeyValuePair<Label, List<SongCard>>(genreNameLabel, songs));

                        //Drawing the label and song cards on screen
                        int cardHeight = genreNameLabel.Height;
                        if ((cardHeight * numOfCardsOnScreen) + 10 <= availableScreenSpace)
                        {
                            genreNameLabel.Location = new Point(0,
                                (searchBar.Location.Y + searchBar.Height) + (cardHeight * numOfCardsOnScreen) + 10);
                            this.Controls.Add(genreNameLabel);
                            ++numOfCardsOnScreen;

                            for (int b = 0; b < songs.Count && (numOfCardsOnScreen * genreNameLabel.Height) <= availableScreenSpace; ++b, ++numOfCardsOnScreen)
                            {
                                songs[b].CardButton.Size = new Size(this.Width, genreNameLabel.Height);
                                songs[b].CardButton.Location = new Point(0, (genreNameLabel.Location.Y) + (cardHeight * (b + 1)));
                                AddSongCardToControlsList(songs[b]);
                            }
                        }
                    }
                }

            }
            else
            {

            }
        }

        private void DrawSearchResultsWindow(string query)
        {
            if(lastWindow != Windows.SONG_PLAYING)
            {
                //Initializing
                int initalCount = this.Controls.Count;
                for (int a = initalCount - 1; a > CARD_START_INDEX; --a)
                {
                    this.Controls.RemoveAt(a);
                }
                numOfCardsOnScreen = 0;
                classifiedCards.Clear();

                //Getting the data
                DataSet data = dbManager.ReturnAllData();
                List<int> songsMatchingQuery = ReturnSearchResults(ref data, query);

                //Getting the already existing controls
                System.Windows.Forms.TextBox searchBar = this.Controls[0] as System.Windows.Forms.TextBox;
                PictureBox searchButton = this.Controls[1] as PictureBox;
                Button[] filterButtons = new Button[5];
                for (int a = 3; a < CARD_START_INDEX; ++a)
                {
                    filterButtons[a - 3] = this.Controls[a] as Button;
                    filterButtons[a - 3].ForeColor = Color.Black;
                }
                VScrollBar scrollBar = this.Controls[this.Controls.Count - 1] as VScrollBar;
                scrollBar.Value = 0;
                scrollBar.Maximum = songsMatchingQuery.Count;

                if (songsMatchingQuery.Count != 0)
                {
                    List<SongCard> songs = new List<SongCard>();
                    GetSongs(ref songsMatchingQuery, ref songs);
                    KeyValuePair<Label, List<SongCard>> sc = new KeyValuePair<Label, List<SongCard>>(null, songs);
                    classifiedCards.Add(sc);

                    int availableScreenSpace = Screen.PrimaryScreen.Bounds.Height
                        - this.Controls[0].Height - this.Controls[CARD_START_INDEX - 1].Height;
                    for (int a = 0; a < songsMatchingQuery.Count && songs[0].CardButton.Size.Height * a < availableScreenSpace; ++a, ++numOfCardsOnScreen)
                    {
                        songs[a].CardButton.Location = new Point(0, (this.Controls[0].Location.Y + this.Controls[0].Height)
                            + (songs[a].CardButton.Height * numOfCardsOnScreen));

                        AddSongCardToControlsList(songs[a]);
                    }
                }
                else
                {

                }
            }
            else
            {

            }
        }

        private void DrawSongPlayingWindow(int songId)
        {
            songPlaying = true;
            
            //Clearing the already present controls
            this.Controls.Clear();

            //Getting the song card
            SongCard sc = GetSong(songId);

            //Getting the song data
            DataRow songData = dbManager.ReturnAllData().Tables[0].Rows[songId];

            this.BackColor = Color.Black;
            if (sc != null)
            {
                //Creating the album art
                PictureBox albumArt = new PictureBox();
                albumArt.Size = new Size(this.Width / 4, this.Height / 2);
                albumArt.Location = new Point((this.Width / 2) - (albumArt.Width / 2),
                    10);
                albumArt.Image = sc.AlbumArt.Image;
                albumArt.SizeMode = PictureBoxSizeMode.StretchImage;
                albumArt.Anchor = AnchorStyles.Top;
                this.Controls.Add(albumArt);

                //Creating the song title label
                Label songTitle = new Label();
                songTitle.Font = new Font(primaryFontName, 28, FontStyle.Bold);
                songTitle.Text = sc.SongNameLabel.Text;
                songTitle.Size = TextRenderer.MeasureText(songTitle.Text, songTitle.Font);
                songTitle.Location = new Point((this.Width / 2) - (songTitle.Width / 2),
                    albumArt.Location.Y + albumArt.Height + 10);
                songTitle.ForeColor = Color.White;
                songTitle.Anchor = AnchorStyles.Top;
                this.Controls.Add(songTitle);

                //Creating the control buttons
                PictureBox[] controlButtons = new PictureBox[5];
                for (byte a = 0; a < 5; ++a)
                {
                    controlButtons[a] = new PictureBox();
                    controlButtons[a].Size = new Size((int)(this.Width * 0.1f), (int)(this.Height * 0.1f));
                    controlButtons[a].SizeMode = PictureBoxSizeMode.StretchImage;
                    controlButtons[a].MouseEnter += OnControlButtonMouseHover;
                    controlButtons[a].MouseLeave += OnControlButtonMouseLeave;
                    if (a != 4)
                    {
                        controlButtons[a].Location = new Point(10 + (a * (controlButtons[a].Width + 50)),
                            this.Height - (controlButtons[a].Height + 50));
                        controlButtons[a].Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
                    }
                    this.Controls.Add(controlButtons[a]);
                }
                controlButtons[0].Name = "PREVIOUS";
                controlButtons[0].Image = Properties.Resources.PreviousSongButton;
                controlButtons[1].Name = "PLAY_PAUSE";
                controlButtons[1].Image = Properties.Resources.PauseButton;
                controlButtons[2].Name = "STOP";
                controlButtons[2].Image = Properties.Resources.StopButton;
                controlButtons[3].Name = "NEXT";
                controlButtons[3].Image = Properties.Resources.NextSongButton;
                controlButtons[4].Name = "BACK";
                controlButtons[4].Image = Properties.Resources.BackButton;
                controlButtons[4].Location = new Point(10, 10);
                controlButtons[4].Anchor = AnchorStyles.Top | AnchorStyles.Left;

                //Creating the volume bar
                TrackBar volBar = new TrackBar();
                volBar.Size = new Size((int)(this.Width * 0.2f), (int)(this.Height * 0.2f));
                volBar.Location = new Point(this.Width - (volBar.Width + 50),
                    this.Height - (volBar.Height + 50));
                volBar.Minimum = 0;
                volBar.Maximum = 100;
                volBar.Value = (int)(userVolume * 100);
                volBar.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
                volBar.ValueChanged += OnVolumeChanged;
                this.Controls.Add(volBar);

                //Creating the progress bar
                TrackBar progressBar = new TrackBar();
                progressBar.Size = new Size(this.Width - 40, 20);
                progressBar.Location = new Point(10,
               (songTitle.Location.Y + songTitle.Height) +
               ((controlButtons[0].Location.Y - (songTitle.Location.Y + songTitle.Height)) / 2));
                progressBar.Minimum = 0;
                progressBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Right | AnchorStyles.Left;
                this.Controls.Add(progressBar);

                /*
                //Creating the progress bar
                PictureBox progressBar = new PictureBox();
                progressBar.Size = new Size(this.Width - 40, 20);
                progressBar.Location = new Point(10,
               (songTitle.Location.Y + songTitle.Height) +
               ((controlButtons[0].Location.Y - (songTitle.Location.Y + songTitle.Height)) / 2));
                progressBar.Image = Properties.Resources.ProgressBar;
                progressBar.SizeMode = PictureBoxSizeMode.StretchImage;
                progressBar.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

                //Creating the progress control
                PictureBox progressControl = new PictureBox();
                progressControl.Size = new Size(20, 20);
                progressControl.Location = progressBar.Location;
                progressControl.Image = Properties.Resources.ProgressControl;
                progressControl.SizeMode = PictureBoxSizeMode.StretchImage;
                this.Controls.Add(progressControl);
                this.Controls.Add(progressBar);
                */

                //Playing the song 
                if (outputDevice == null)
                {
                    outputDevice = new WaveOutEvent();
                    outputDevice.PlaybackStopped += OnPlaybackStopped;
                }
                if (audioFile == null)
                {
                    audioFile = new AudioFileReader(dbManager.ReturnFilePath(songId));
                    outputDevice.Init(audioFile);
                }
                outputDevice.Volume = userVolume;
                progressBar.Maximum = (int)audioFile.TotalTime.TotalSeconds;
                outputDevice.Play();
                parallelThread = new Thread(new ParameterizedThreadStart(TrackSongPosition));
                parallelThread.Start(progressBar);
            }

        }

        #endregion

        #region Events

        private void OnSearchButtonHover(object sender, EventArgs e)
        {
            PictureBox searchButton = sender as PictureBox;
            if (searchButton != null)
            {
                searchButton.Image = Properties.Resources.SearchHover;
            }
        }

        private void OnSearchButtonMouseLeave(object sender, EventArgs e)
        {
            PictureBox searchButton = sender as PictureBox;
            if (searchButton != null)
            {
                searchButton.Image = Properties.Resources.SearchNonHover;
            }
        }

        private void OnSearchButtonClicked(object sender, EventArgs e)
        {
            lastWindow = currentWindow;
            currentWindow = Windows.SEARCH_RESULTS;

            System.Windows.Forms.TextBox tb = this.Controls[0] as System.Windows.Forms.TextBox;

            DrawSearchResultsWindow(tb.Text);
        }

        private void OnFilterButtonHover(object sender, EventArgs e)
        {
            Button filterButton = sender as Button;
            if (filterButton != null)
                filterButton.ForeColor = Color.White;
        }

        private void OnFilterButtonMouseLeave(object sender, EventArgs e)
        {
            Button filterButton = sender as Button;
            if (filterButton != null)
            {
                switch (currentWindow)
                {
                    case Windows.ALL_SONG_LIST:
                        if (filterButton.Name != "all_F_B")
                            filterButton.ForeColor = Color.Black;
                        break;
                    case Windows.ARTIST_SONG_LIST:
                        if (filterButton.Name != "artists_F_B")
                            filterButton.ForeColor = Color.Black;
                        break;
                    case Windows.ALBUM_SONG_LIST:
                        if (filterButton.Name != "albums_F_B")
                            filterButton.ForeColor = Color.Black;
                        break;
                    case Windows.GENRES_SONG_LIST:
                        if (filterButton.Name != "genres_F_B")
                            filterButton.ForeColor = Color.Black;
                        break;
                    default: filterButton.ForeColor = Color.Black; break;
                }
            }
        }

        private void OnFilterButtonClicked(object sender, MouseEventArgs args)
        {
            Button filterButton = sender as Button;
            if (filterButton != null)
            {
                filterButton.ForeColor = Color.White;
                switch (currentWindow)
                {
                    case Windows.ALL_SONG_LIST: this.Controls[CARD_START_INDEX - 4].ForeColor = Color.Black; break;
                    case Windows.ARTIST_SONG_LIST: this.Controls[CARD_START_INDEX - 3].ForeColor = Color.Black; break;
                    case Windows.ALBUM_SONG_LIST: this.Controls[CARD_START_INDEX - 2].ForeColor = Color.Black; break;
                    case Windows.GENRES_SONG_LIST: this.Controls[CARD_START_INDEX - 1].ForeColor = Color.Black; break;
                }

                //Switching Windows
                if (filterButton.Name == "all_F_B" && currentWindow != Windows.ALL_SONG_LIST)
                {
                    SwitchWindow(Windows.ALL_SONG_LIST);
                }
                else if (filterButton.Name == "artists_F_B" && currentWindow != Windows.ARTIST_SONG_LIST)
                {
                    SwitchWindow(Windows.ARTIST_SONG_LIST);
                }
                else if (filterButton.Name == "albums_F_B" && currentWindow != Windows.ALBUM_SONG_LIST)
                {
                    SwitchWindow(Windows.ALBUM_SONG_LIST);
                }
                else if (filterButton.Name == "genres_F_B" && currentWindow != Windows.GENRES_SONG_LIST)
                {
                    SwitchWindow(Windows.GENRES_SONG_LIST);
                }
            }
        }

        private void OnMusicScrollBarScroll(object sender, EventArgs args)
        {
            VScrollBar scrollBar = sender as VScrollBar;
            if (scrollBar != null)
            {
                Scroll(scrollBar.Value);

                //Updating the mouse scroll counter
                mouseScrollCounter = scrollBar.Value;
            }
        }

        private void OnMouseScroll(object sender, MouseEventArgs args)
        {
            if (currentWindow != Windows.SONG_PLAYING)
            {
                mouseScrollCounter += -1 * (args.Delta / SystemInformation.MouseWheelScrollDelta);
                VScrollBar scrollBar = this.Controls[CARD_START_INDEX] as VScrollBar;

                if (mouseScrollCounter < 0)
                    mouseScrollCounter = 0;
                else if (currentWindow == Windows.ALL_SONG_LIST && mouseScrollCounter > songCards.Count - numOfCardsOnScreen)
                {
                    mouseScrollCounter = songCards.Count - numOfCardsOnScreen;
                    return;
                }
                else if (currentWindow != Windows.ALL_SONG_LIST && mouseScrollCounter > scrollBar.Maximum - numOfCardsOnScreen)
                {
                    mouseScrollCounter = scrollBar.Maximum - numOfCardsOnScreen;
                    return;
                }

                scrollBar.Value = mouseScrollCounter;
            }
        }

        private void OnCardButtonClicked(object sender, MouseEventArgs e)
        {
            Button cardButton = sender as Button;
            if(cardButton != null)
            {
                PlaySong(Convert.ToInt32(cardButton.Name));
            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs args)
        {
            parallelThread.Abort();
            parallelThread = null;
            outputDevice.Dispose();
            outputDevice = null;
            audioFile.Dispose();
            audioFile = null;    
        }

        private void OnVolumeChanged(object sender, EventArgs args)
        {
            TrackBar volBar = sender as TrackBar;
            if(volBar != null)
            {
                if (outputDevice != null)
                {
                    userVolume = (float)volBar.Value / 100.0f;
                    outputDevice.Volume = userVolume;
                }
            }
        }

        private void OnControlButtonMouseHover(object sender, EventArgs args)
        {
            PictureBox controlButton = sender as PictureBox;
            if(controlButton != null)
            {
                SizeF scalingFactor = new SizeF(1.1f, 1.1f);
                controlButton.Scale(scalingFactor);
                controlButton.Location = new Point((int)(controlButton.Location.X / scalingFactor.Width),
                    (int)(controlButton.Location.Y / scalingFactor.Height));
            }
        }

        private void OnControlButtonMouseLeave(object sender, EventArgs args)
        {
            PictureBox controlButton = sender as PictureBox;
            if(controlButton != null)
            {
                SizeF scalingFactor = new SizeF(1f / 1.1f, 1f / 1.1f);
                controlButton.Scale(scalingFactor);
                controlButton.Location = new Point((int)(controlButton.Location.X / scalingFactor.Width),
                    (int)(controlButton.Location.Y / scalingFactor.Height));
            }
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            dbManager.ExitPreprocessing();
            BeforeClosing();

            if (songPlaying)
                OnPlaybackStopped(null,null);

            //Saving the user preferences
            StreamWriter stream = new StreamWriter(INI_FILE_ADDR);
            stream.WriteLine(MUSIC_FOLDER);
            stream.WriteLine(userVolume.ToString());
            stream.Close();
        }

        #endregion

        #region Event Helpers

        private void Scroll(int scrollValue)
        {
            if (currentWindow == Windows.ALL_SONG_LIST)
            {
                //Resetting the card counter
                numOfCardsOnScreen = 0;

                //Accounting for any change in control sizes due to resizing of windows 
                Size currentSongCardSize = this.Controls[this.Controls.Count - 1].Size;

                //Clearing the already existing song cards
                int initialControlsCount = this.Controls.Count;
                for (int a = initialControlsCount - 1; a > CARD_START_INDEX; --a)
                {
                    this.Controls.RemoveAt(a);
                }

                int availableScreenSpace = Screen.PrimaryScreen.Bounds.Height
                    - this.Controls[0].Height - this.Controls[CARD_START_INDEX - 1].Height;
                for (int b = scrollValue; b < songCards.Count && currentSongCardSize.Height * (b - scrollValue) < availableScreenSpace; ++b, ++numOfCardsOnScreen)
                {
                    if (b == scrollValue)
                        songCards[b].CardButton.Location = new Point(0, Controls[0].Location.Y + Controls[0].Height + 10);
                    else
                        songCards[b].CardButton.Location = new Point(0, songCards[b - 1].CardButton.Location.Y + songCards[b - 1].CardButton.Height);

                    songCards[b].CardButton.Size = currentSongCardSize;
                    AddSongCardToControlsList(songCards[b]);
                }
            }
            else if(currentWindow == Windows.SEARCH_RESULTS)
            {
                //Resetting the card counter
                numOfCardsOnScreen = 0;

                //Accounting for any change in control sizes due to resizing of windows 
                Size currentCardSize = this.Controls[this.Controls.Count - 1].Size;

                //Clearing the already existing song cards
                for (int a = this.Controls.Count - 1; a > CARD_START_INDEX; --a)
                {
                    this.Controls.RemoveAt(a);
                }

                //Adding the new cards
                int availableScreenSpace = Screen.PrimaryScreen.Bounds.Height
                    - this.Controls[0].Height - this.Controls[CARD_START_INDEX - 1].Height;
                for(int a = scrollValue; a < classifiedCards[0].Value.Count && currentCardSize.Height * numOfCardsOnScreen < availableScreenSpace; ++a, ++numOfCardsOnScreen)
                {
                    classifiedCards[0].Value[a].CardButton.Size = currentCardSize;
                    classifiedCards[0].Value[a].CardButton.Location = new Point(0,
                        (this.Controls[0].Location.Y + this.Controls[0].Height) +
                        (currentCardSize.Height * numOfCardsOnScreen));

                    AddSongCardToControlsList(classifiedCards[0].Value[a]);
                }
            }
            else
            {
                //Resetting the card counter
                numOfCardsOnScreen = 0;

                //Accounting for any change in control sizes due to resizing of windows 
                Size currentCardSize = this.Controls[this.Controls.Count - 1].Size;

                //Clearing the already existing song cards
                for (int a = this.Controls.Count - 1; a > CARD_START_INDEX; --a)
                {
                    this.Controls.RemoveAt(a);
                }

                //Adding the new cards
                int availableScreenSpace = Screen.PrimaryScreen.Bounds.Height
                    - this.Controls[0].Height - this.Controls[CARD_START_INDEX - 1].Height;
                for (int a = 0, counter = 0; a < classifiedCards.Count && currentCardSize.Height * numOfCardsOnScreen < availableScreenSpace; ++a)
                {

                    if (counter >= scrollValue)
                    {
                        classifiedCards[a].Key.Size = currentCardSize;
                        classifiedCards[a].Key.Location = new Point(0, (this.Controls[0].Location.Y +
                            this.Controls[0].Height) + (currentCardSize.Height * numOfCardsOnScreen));

                        this.Controls.Add(classifiedCards[a].Key);
                        ++numOfCardsOnScreen;

                        for (int b = 0; b < classifiedCards[a].Value.Count && currentCardSize.Height * numOfCardsOnScreen < availableScreenSpace; ++b, ++numOfCardsOnScreen)
                        {
                            classifiedCards[a].Value[b].CardButton.Size = currentCardSize;
                            classifiedCards[a].Value[b].CardButton.Location = new Point(0, (this.Controls[0].Location.Y +
                            this.Controls[0].Height) + (currentCardSize.Height * numOfCardsOnScreen));
                            AddSongCardToControlsList(classifiedCards[a].Value[b]);
                        }
                    }
                    else
                    {
                        ++counter;
                        for (int b = 0; b < classifiedCards[a].Value.Count && currentCardSize.Height * numOfCardsOnScreen < availableScreenSpace; ++b, ++counter)
                        {
                            if (counter >= scrollValue)
                            {
                                classifiedCards[a].Value[b].CardButton.Size = currentCardSize;
                                classifiedCards[a].Value[b].CardButton.Location = new Point(0, (this.Controls[0].Location.Y +
                                     this.Controls[0].Height) + (currentCardSize.Height * numOfCardsOnScreen));
                                AddSongCardToControlsList(classifiedCards[a].Value[b]);
                                ++numOfCardsOnScreen;
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}

