using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;
using System.Reflection;
using System.Configuration;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Speech.Synthesis;
using OpenMetaverse;
using OpenMetaverse.Voice;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LinqToTwitter;
using OpenNLP;
using OpenNLP.Tools.Tokenize;
using OpenNLP.Tools.PosTagger;
using OpenNLP.Tools.NameFind;
using OpenNLP.Tools.Parser;
using OpenNLP.Tools.Chunker;

namespace WindowsFormsApplication1
{
    public partial class MockingBOT : Form
    {
        // ALL OF THE VARIABLES!
        static Random rnd = new Random();

        public static GridClient Client = new GridClient();

        public static SpeechSynthesizer Speech = new SpeechSynthesizer();
        public static bool speakingaloud = true;

        private static BotStorage l = new BotStorage(); // The actual file
        private static botSettings s = new botSettings(); // The bot loaded into memory. Kinda useless - further optimization would probably remove this.
        // In the Region Module, there's a class called BotOperator that contains all the miscellaneous data below, for use in operating multiple bots at once. The client, however, never does this, so that class isn't used here. Those variables are here.
        
        private static UUID FayeN = new UUID("1d3ae678-1669-4a8a-883c-ae49d1678d99"); // This helps send messages to my alt for debugging purposes.
        private static UUID LilyB = new UUID("f2c7a3cb-6de6-431a-858c-95188d946917");
        
        private string file = ""; // This is for remembering where our file is stored, for save/load.

        static bool hasconnection = false; // Whether or not we're connected to the grid.
        static bool following = new bool(); // Whether or not this bot is in 'stalker' mode.
        static UUID tofollow = new UUID(); // Who we're following.
        static int followtime = 0; // How long we've been following (for timing whether or not to ask for stories, etc.)
        static List<UUID> greetlist = new List<UUID>(); // People we have greeted.
        static List<UUID> noticelist = new List<UUID>(); // People we have noticed.
        static Stack<UUID> alist = new Stack<UUID>(); // Access list

        // This is for when you grab a node under the 'Brain' tab. Keeps track of where you grab it so it moves properly with the cursor.
        static int grabx = 0;
        static int graby = 0;

        // This is where the draw area starts. For scrolling.
        static int drawx = 0;
        static int drawy = 0;

        static int mxPrev = 0;
        static int myPrev = 0;

        bool ready = false; // Whether or not connected, and also, the bot is ready to be used.

        static Dictionary<UUID, int> anims = new Dictionary<UUID, int>(); // For all the animations the bot is using.

        static string name = ""; // For debugging - the name of the bot this is tied to, just for making sure we have the right one.
        
        static int yesno = -1; // For getting responses.
        static bool senttele = false; // Whether or not a teleport request was sent.
        static bool shutup = false; // Whether or not asked not to send chat messages

        // For flood detection. Shuts it up if too many messages were sent.
        static int timesmsgd = 0;
        static int flooddetect = 0;
        static int floodcd = 500; // 30 seconds
        static int floodcdt = floodcd;

        static string currentcity = ""; // For geolocating.

        static int state = 0; // Finite state that determines what responses to pull out.
        
        static List<string> speech = new List<string>();

        static int limit = 1000; // Limit on characters!
        static List<textNode> used = new List<textNode>();

        static int nodeselp = -1;
        static int nodesel = -1;
        static int drawType = 0;

        // Various, self-explanatory variables for listening to stories, and telling them.
        static bool listening = false;
        static bool storytelling = false;
        static botStories currentstory = null;
        static int storystage = 0;
        static float storywait = 0; // Waiting until telling the next paragraph.
        static UUID listener = new UUID();
        static bool storywaituntilover = false; // Whether or not it's allowed to finish their story before they say 'goodbye', if hitchhiking and has reached its destination.
        static int listeningtime = 0; // For gauging how long it's been since the user last told part of the story.
        static bool endcheck = false; // For checking if it's the end of the story.

        // Various variables for tourist stuff.
        static bool touring = false; // Giving a tour?
        static int tourWait = 0; // Waiting for someone?
        static Vector3 tourLoc = new Vector3(); // Current location
        static UUID tourist = new UUID(); // Who's the tourist?
        static bool touristPresent = false; // Are they present?
        static int tourStage = 0; // What stage are we on?
        static int tstoryStage = 0; // What stage of the information are we telling?
        static bool learningtour = false; // Are we learning a tour?
        static botTours currenttour = null; // What tour are we recalling (by ID)
        static int tourstageedit = 0; // What stage are we on (when learning)

        static bool learningroutine = false; // Are we learning a routine?
        static int currentroutine = 0; // What routine are we on?
        static int routinestage = -1; // What stage?

        static Vector3 prevPosition = new Vector3(); // Position when last checked, for tracking distance moved.
        static Vector3 currentdest = new Vector3(); // Where we're supposed to be going.

        static int movetimeout = 0;
        static bool waiting = false;
        static int askWait = 0;

        static Vector3 wanderDest = Vector3.Zero; // Where to go when wandering.
        static int idleTime = 0; // Time to spend at destination before walking off again.
        static int stuckTime = 0; // Count up when the avatar is attempting to move, but isn't getting anywhere (therefore, is stuck on a wall or something)

        // Stores the various locations of the tour spots, indexed by stage
        static List<Vector3> tourLocs = new List<Vector3>();

        // Last thing we heard.
        static string lastresp = "";
        static UUID lastheard = new UUID();
        static string lastIM = "";
        static UUID lastIMer = new UUID();

        public Point Dp1 = new Point(); //  draw start point
        public Point Dp2 = new Point(); // End point
        public int Drawing = -1; // What are we drawing? Is it pretty?

        // For all the various drawing. The font could probably stand to be changed to something more open-source friendly.
        Graphics gra;
        Bitmap g = new Bitmap(1000, 1000);
        Font f = new Font("Microsoft Sans Serif", 8);

        static Vector3 hhdest = Vector3.Zero; // Where are we hitchhiking to?
        static string hhcity = ""; // What city?
        static List<Vector3> hhdests = new List<Vector3>();

        // For linguistic stuff. This could be moved, but I felt it would be used more than once.
        static string[] conjunctions = new string[] { "and", "for", "nor", "but", "or", "yet", "so" };
 
        // For Twitter!
        static SingleUserAuthorizer auth = new SingleUserAuthorizer
        {
            Credentials = new InMemoryCredentials
            {
                ConsumerKey = "WQ6Bg40bf2KnB7LPI1kA",
                ConsumerSecret = "Ks5GweyawGi2XAPiGIHogXwh5Vk1mjBZXKz3injY",
                OAuthToken = "1551038294-7lTsCSjteD9LBD1vX1lOrdi2AXLXU1oPRy9UrkZ",
                AccessToken = "uZ3KHHpOHV3oqOqdbG2MAhyDXGaTrExwbqkjaML5uA",
            }
        };
        static TwitterContext twit = new TwitterContext(auth);

        static string mModelPath = new System.Uri( System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase) ).LocalPath + @"\Models\";
        /*
        EnglishMaximumEntropyTokenizer tokenizer = new EnglishMaximumEntropyTokenizer(mModelPath + "EnglishTok.nbin");
        EnglishMaximumEntropyPosTagger posTagger = new EnglishMaximumEntropyPosTagger(mModelPath + "EnglishPOS.nbin");
        EnglishTreebankParser parser = new EnglishTreebankParser(mModelPath,true,false);
        EnglishNameFinder nameFinder = new EnglishNameFinder(mModelPath);
        EnglishTreebankChunker chunker = new EnglishTreebankChunker(mModelPath + "EnglishChunk.nbin");
        */
        public MockingBOT()
        {
            InitializeComponent();

            //twit.UpdateStatus("woo");

            // Let's create us a timer. 
            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Elapsed += new ElapsedEventHandler(FracSecond);
            // Set the Interval to 1/20th of a second.
            aTimer.Interval = 50;
            aTimer.Enabled = true;

            System.Timers.Timer sTimer = new System.Timers.Timer();
            sTimer.Elapsed += new ElapsedEventHandler(HalfSecond);
            // Set the Interval to every half a second (and keep it called 'PerSecond' because I'm an idiot).
            sTimer.Interval = 300;
            sTimer.Enabled = true;

            System.Timers.Timer mTimer = new System.Timers.Timer();
            mTimer.Elapsed += new ElapsedEventHandler(HalfMinute);
            // Set the Interval to every half a minute.
            mTimer.Interval = 30000;
            mTimer.Enabled = true;

            following = false;
            ready = false;

            movementType1.SelectedIndex = 0;
            s.moveType = 0;
            botType1.SelectedIndex = 0;
            s.botType = 0;

            Vector3 newdest = new Vector3();
            newdest.X = rnd.Next(255); newdest.Y = rnd.Next(255); newdest.Z = 0;
            hhdests.Add(newdest);

            Client.Settings.SEND_AGENT_APPEARANCE = true;

            this.BringToFront();
        }
        public void MockingBOT_Load(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog yay = new OpenFileDialog();
                yay.Title = "MockingBOT";
                Directory.SetCurrentDirectory(Environment.CurrentDirectory + "\\data");
                yay.InitialDirectory = Environment.CurrentDirectory;
                if (!Directory.Exists(yay.InitialDirectory))
                {
                    Directory.CreateDirectory(yay.InitialDirectory);
                }
                yay.Filter = "Xml Files (*.xml)|*.xml";
                if (yay.ShowDialog() == DialogResult.OK)
                {
                    LoadSettings(yay.FileName);
                    this.Refresh();
                }
                //LoadSettings(Environment.CurrentDirectory + "\\data\\settings.xml");
                //LoadNodes(Environment.CurrentDirectory + "\\data\\nodes.xml");
                //LoadTours(Environment.CurrentDirectory + "\\data\\tours.xml");
                //LoadStories(Environment.CurrentDirectory + "\\data\\stories.xml");
            }
            catch { }
            gra = Graphics.FromImage(g);

            if (l.bots.Count == 0)
            {
                l.bots.Add(new botSettings());
                s = l.bots[l.bots.Count - 1];
            }

            List<string> botnamelist = new List<string>();
            foreach (botSettings b in l.bots) { botnamelist.Add(b.name); }
            botListBox1.DataSource = botnamelist;

            checkBox2.Checked = speakingaloud;

            s.alist.Add(FayeN); // Add myself to the access list. This can be imported from external data later. For now it's here to keep things contained to me.
            s.alist.Add(LilyB);
            s.alist.Add(new UUID("9b79b5cf-a511-4019-834c-111ca5905538")); // Ian Murray

            accessList1.DataSource = s.alist;

            loginURL.Text = s.customLogin;

            if (s.speech.Count == 0)
            {
                s.speechnames.Add("Default");
                defaultSpeechAdd();
            }
            if (s.sbindings.Count <= 0)
            {
                s.sbindings.Add("Greeting");
                s.sbindings.Add("Ask to follow");
                s.sbindings.Add("Confirm follow");
                s.sbindings.Add("Deny follow");
                s.sbindings.Add("Asked to follow; already following");
                s.sbindings.Add("Asked to stop following");
                s.sbindings.Add("Askd to stop following; not following");
                s.sbindings.Add("Ask for a story");
                s.sbindings.Add("Hearing story");
                s.sbindings.Add("Hearing story 2");
                s.sbindings.Add("Hearing story 3");
                s.sbindings.Add("Hearing story; ask if over");
                s.sbindings.Add("Hearing story; story over");
                s.sbindings.Add("Hearing story; story not over");
                s.sbindings.Add("Tell story; success");
                s.sbindings.Add("Tell story; no stories");
                s.sbindings.Add("Telling story; story over");
                s.sbindings.Add("Go to tour location");
                s.sbindings.Add("Tourist teleport");
                s.sbindings.Add("Tour end");
                s.sbindings.Add("Reached tour spot; begin explanation");
                s.sbindings.Add("Reached tour spot; greet latecomer tourist; begin explanation");
                s.sbindings.Add("Tourist late; ask to stop tour");
                s.sbindings.Add("Tourist late; don't stop");
                s.sbindings.Add("Tourist late; stop");
                s.sbindings.Add("End tour spot; more to go");
                s.sbindings.Add("End tour spot; last spot");
                s.sbindings.Add("Stop tour");
                s.sbindings.Add("Stop tour; wasn't touring");
                s.sbindings.Add("Flood detected");
                s.sbindings.Add("Ask to teleport");
                s.sbindings.Add("Asked to start learning tour");
                s.sbindings.Add("Point out tourist spot");
                s.sbindings.Add("Hear story about tourist spot");
                s.sbindings.Add("Hear story about tourist spot 2");
                s.sbindings.Add("Hear story about tourist spot 3");
                s.sbindings.Add("Stop teaching");
                s.sbindings.Add("Stop teaching; wasn't learning");
                s.sbindings.Add("Not on access list");
                s.sbindings.Add("Doesn't have response");
                s.sbindings.Add("Happy exclamation");
                s.sbindings.Add("Happy exclamation 2");
                s.sbindings.Add("'All right'; spoken");
                s.sbindings.Add("'All right'; exclamation");
                s.sbindings.Add("Rejected 1");
                s.sbindings.Add("Rejected 2"); // 45
                s.sbindings.Add("Sad exclamation");
                s.sbindings.Add("Thank you");
                s.sbindings.Add("");
                s.sbindings.Add("");
                s.sbindings.Add("");
                s.sbindings.Add(""); // 50
                s.sbindings.Add("");
                s.sbindings.Add("");
                s.sbindings.Add("");
                s.sbindings.Add(""); // 55
                s.sbindings.Add("");
                s.sbindings.Add("");
                s.sbindings.Add("");
                s.sbindings.Add("");
                s.sbindings.Add("Ask to tell story"); // 60
                s.sbindings.Add("Hitchhiking; reached destination");
                s.sbindings.Add("Hitchhiking; farewell");
                s.sbindings.Add("Hitchhiking; Ask to finish story");
                s.sbindings.Add("Sees person; waves");
                s.sbindings.Add("Person passes by"); // 65
                s.sbindings.Add("Tell where going");
                s.sbindings.Add("Tell where going; city only");
                s.sbindings.Add("Note city");
                s.sbindings.Add("Greeting a known person");
                s.sbindings.Add("Ask to give tour"); // 70 
                s.sbindings.Add("Tour refused");
                s.sbindings.Add("No tours available");
                s.sbindings.Add("Asked to stop following; wasn't");
                s.sbindings.Add("Asked to stop talking");
                s.sbindings.Add("Tweeted"); // 75
                s.sbindings.Add("Can't tweet; too long");
                s.sbindings.Add("Weather");
                s.sbindings.Add("Ask to share story");
                s.sbindings.Add("");
                s.sbindings.Add("Improper command"); // 80
            }

            speechSettings.DataSource = s.speechnames;
            speechList.DataSource = s.sbindings;

            gridSelection.SelectedIndex = 0;

            Client.Network.SimConnected += new EventHandler<SimConnectedEventArgs>(Network_SimConnected);
            Client.Network.SimDisconnected += new EventHandler<SimDisconnectedEventArgs>(Network_SimDisconnected);
            Client.Avatars.UUIDNameReply += new EventHandler<UUIDNameReplyEventArgs>(Avatars_UUIDNameReply);

            // This allows us to use TerrainHeightAtPoint.
            Client.Settings.STORE_LAND_PATCHES = true;
        }
        private void MockingBOT_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Client.Network.Connected) { Client.Network.Logout(); }
            if (file != "")
            {
                DialogResult foo = MessageBox.Show("Save everything?", "MockingBOT", MessageBoxButtons.YesNo);
                if (foo == DialogResult.Yes)
                {
                    //SaveSettings(Environment.CurrentDirectory + "\\data\\default.xml");
                    SaveSettings(file);
                }
            }
        }

        // Various timing events, in order of shortest to longest intervals.
        private void FracSecond(object source, ElapsedEventArgs e)
        {
            // Frac second: 1/20th of a second (effectively 20 fps)
            // Use this VERY sparingly, as it will effectively run constantly, and keep in mind that Second Life/OpenSim is naturally laggy.
            try
            {
                foreach (botSettings b in l.bots)
                {
                    for (int i = 0; i < b.mLearning.Count; i++)
                    {
                        if (b.mLearning[i].Count < l.nodes.Count)
                        {
                            int foo = l.nodes.Count - b.mLearning[i].Count;
                            for (int ii = 0; ii < foo; ii++)
                            {
                                b.mLearning[i].Add(0);
                            }
                        }
                    }
                }
            }
            catch { }

            if (timesmsgd >= 12)
            {
                if (flooddetect == 0) { BotIM(LilyB, getSpeech(29)); };
                flooddetect = 1;
            }
            floodcdt--;
            if (floodcdt < 0)
            { 
                // Reset that timer.
                timesmsgd = 0;
                floodcdt = floodcd;
                if (flooddetect != 0) { flooddetect = 0; }
            }

            if (tourWait == 0) { senttele = false; }
            if (storytelling == false)
            {
                storystage = 0;
                storywait = 0;
            }

            SetText(idleLocLabel1, "Idle Location: " + s.idleLoc.ToString()); // Update the client.
        }
        private void HalfSecond(object source, ElapsedEventArgs e)
        {
            if (ready == true)
            {
                SetText(currentLocationLabel, "Current Location: " + Client.Self.SimPosition.ToString());
                SetText(currentDestLabel, "Current Destination: " + currentdest.ToString());
                SetText(wanderDestLabel, "'Wander' Destination: " + wanderDest.ToString());
            }

            if (askWait > 0) { askWait--; }
            if (movetimeout > 0) { movetimeout--; }

            if (ready == true && Client.Self.Velocity == Vector3.Zero && hhdest == Vector3.Zero) { randomizeDestination(); }
            if (following == false) { followtime = 0; }

            if (storytelling == true && waiting == false)
            {
                storywait++;
                String whoop = String.Empty;
                if (currentstory.paragraphs.Count > storystage)
                {
                    whoop = currentstory.paragraphs[storystage];
                    int len = whoop.Split(' ').Length;
                    if (len < 3 + (storywait) * Math.Sqrt(len)) // 1.5 for 90 WPM
                    {
                        UUID typing = new UUID("c541c47f-e0c0-058b-ad1a-d6ae3a4584d9");
                        Client.Self.AnimationStop(typing, false);
                        storywait = 0;
                        storystage++;
                        BotSpeak(whoop, false);
                    }
                    else
                    {
                        UUID typing = new UUID("c541c47f-e0c0-058b-ad1a-d6ae3a4584d9");
                        Client.Self.AnimationStart(typing, true);
                    }
                }
                else
                {
                    storytelling = false;
                    storystage = 0;
                    if (storywaituntilover) { BotSpeak(getSpeech(16), false); }
                    else { BotSpeak(getSpeech(16)); }
                }
            }
            else { storywaituntilover = false; }

            if (touring == true)
            {
                if (currenttour.tourLocs.Count > tourStage)
                {
                    tourLoc = currenttour.tourLocs[tourStage];
                    if (Vector3.Distance(Client.Self.SimPosition, tourLoc) <= 8)
                    {
                        if (touristPresent == false) { tourWait++; waitForTourist(tourist); } // Waiting routine.
                        else // TOUR COMMENCE
                        {
                            tourWait = 0;
                            if (currenttour.tourInfo[tourStage].Count > tstoryStage)
                            {
                                if (tstoryStage == 0) { Client.Self.AnimationStop(Animations.POINT_YOU, true); Client.Self.AnimationStart(Animations.POINT_YOU, true); }
                                string woo = currenttour.tourInfo[tourStage][tstoryStage];
                                tstoryStage++;
                                BotSpeak(woo);
                            }
                            else if (currenttour.tourInfo[tourStage].Count == tstoryStage)
                            {
                                if (currenttour.tourLocs.Count > tourStage + 1)
                                {
                                    tstoryStage++;
                                    BotSpeak(getSpeech(25));
                                }
                                else
                                {
                                    tstoryStage++;
                                    BotSpeak(getSpeech(26));
                                }
                            }
                        }
                    }
                    else if (movetimeout <= 0)
                    {
                        goTo(tourLoc);
                        movetimeout = 12;
                        //Client.Self.AutoPilotLocal((int)tourLoc.X, (int)tourLoc.Y, (int)tourLoc.Z); // Keep putting us en route.
                    }
                }
                else
                {
                    touring = false;
                    BotSpeak(getSpeech(19));
                }
            }
            else if (following == true)
            {
                followtime++;

                try
                {
                    OpenMetaverse.Vector3 pos = Client.Self.SimPosition;
                    OpenMetaverse.Vector3 pos2 = new Vector3();
                    Avatar A = Client.Network.CurrentSim.ObjectsAvatars.Find(delegate(Avatar Av) { return Av.ID == tofollow; });
                    pos2 = A.Position;

                    if (Vector3.Distance(pos, pos2) > 2)
                    { goTo(pos2); }

                    if (Vector3.Distance(pos, pos2) >= 30) { Client.Self.Teleport(Client.Network.CurrentSim.Name, pos2); }
                    else if (Vector3.Distance(pos, pos2) >= 10) { Client.Self.Movement.AlwaysRun = true; }
                    else { Client.Self.Movement.AlwaysRun = false; }

                    Client.Self.Movement.Camera.LookAt(pos2, pos2);
                    Client.Self.Movement.TurnToward(pos2);

                    if (hhdest != Vector3.Zero && askWait <= 0 && Vector3.Distance(pos, hhdest) > 10)
                    {
                        if (s.botType == 0)
                        {

                            if (rnd.Next(1000) <= 30) // Ask to hear story
                            {
                                askWait = 100;
                                if (A.Velocity == Vector3.Zero && waiting == false && currentstory == null)
                                {
                                    //DoAnimation(Animations.);
                                    BotSpeak(getSpeech(7), true); //"Say... wanna tell me a story?"
                                    if (waitForAnswer(false) == true)
                                    {
                                        DoAnimation(Animations.CLAP);
                                        BotSpeak(getSpeech(40), false);
                                        askForStory(tofollow);
                                    }
                                    else
                                    {
                                        DoAnimation(Animations.SHRUG);
                                        BotSpeak(getSpeech(44), false);
                                    }
                                }
                            }
                            else if (rnd.Next(1000) <= 5 && l.stories.Count > 0) // Ask to tell one
                            {
                                askWait = 100;
                                if (waiting == false && currentstory == null)
                                {
                                    //DoAnimation(Animations.);
                                    BotSpeak(getSpeech(60), true);
                                    if (waitForAnswer(false) == true)
                                    {
                                        DoAnimation(Animations.CLAP);
                                        BotSpeak(getSpeech(41), false);
                                        tellStory(tofollow);
                                        System.Threading.Thread.Sleep(2000);
                                    }
                                    else
                                    {
                                        DoAnimation(Animations.SHRUG);
                                        BotSpeak(getSpeech(44), false);
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    following = false; tofollow = UUID.Zero;
                }

                if (Vector3.Distance(Client.Self.SimPosition, hhdest) <= 10 && waiting == false) // At destination! (Vector3.Distance(Client.Self.SimPosition, hhdest) <= 10 )
                {
                    bool stop = true;

                    if (storytelling)
                    {
                        if (storywaituntilover == false)
                        {
                            DoAnimation(Animations.JUMP_FOR_JOY);
                            BotSpeak(getSpeech(61), false);
                            System.Threading.Thread.Sleep(3000);
                            BotSpeak(getSpeech(63), false);
                            if (waitForAnswer(false) == false) { BotSpeak(getSpeech(42), false); }
                            else
                            {
                                storywaituntilover = true;
                                stop = false;
                                BotSpeak(getSpeech(43), false);
                            }
                        }
                        else { stop = false; }
                    }
                    else
                    {
                        DoAnimation(Animations.JUMP_FOR_JOY);
                        BotSpeak(getSpeech(61), false);
                    }

                    if (stop)
                    {
                        following = false;
                        System.Threading.Thread.Sleep(3000);
                        DoAnimation(Animations.HELLO);
                        BotSpeak(getSpeech(62), false);
                        System.Threading.Thread.Sleep(3000);
                        goTo(hhdest);
                        hhdest = Vector3.Zero;
                    }
                }
            }
            else // Idle!
            {
                if (ready == true)
                {
                    bool began = false;

                    if (idleTime > 0 && noticelist.Count == 0) { idleTime--; if (idleTime == 0) { began = true; } }

                    if (s.moveType == 2 && noticelist.Count == 0) // Routine!
                    {
                        if (idleTime <= 0)
                        {
                            if (currentroutine == -1 && l.routines.Count > 0)
                            {
                                currentroutine = rnd.Next(l.routines.Count);
                            }
                            if (currentroutine != -1)
                            {
                                if (routinestage == -1 && l.routines[currentroutine].routineLocs.Count > 0)
                                {
                                    routinestage++;
                                    BotSpeak(l.routines[currentroutine].routineSpeech[routinestage], false);
                                    wanderDest = l.routines[currentroutine].routineLocs[routinestage];
                                }
                            }

                            if (currentroutine != -1 && routinestage != -1)
                            {
                                if (began)
                                {
                                    if (l.routines[currentroutine].routineSpeech[routinestage] != "")
                                        BotSpeak(l.routines[currentroutine].routineSpeech[routinestage], false);
                                }

                                if (wanderDest == Vector3.Zero && l.routines[currentroutine].routineLocs[routinestage] != Vector3.Zero)
                                    wanderDest = l.routines[currentroutine].routineLocs[routinestage];

                                if (Vector3.Distance(Client.Self.SimPosition, wanderDest) < 4 || (wanderDest == Vector3.Zero && l.routines[currentroutine].routineLocs[routinestage] == Vector3.Zero))
                                {
                                    idleTime = l.routines[currentroutine].routinePauses[routinestage] * 2; // *2 because this event actually runs every half a second, not every second...
                                    wanderDest = Vector3.Zero;

                                    if (l.routines[currentroutine].routineLocs.Count > routinestage + 1) { routinestage++; }
                                    else if (l.routines[currentroutine].looped) { routinestage = 0; }
                                    else { currentroutine = -1; routinestage = -1; }
                                    //MessageBox.Show(routinestage.ToString()); 

                                    // Changed routine! Unless didn't.
                                    if (currentroutine != -1)
                                        wanderDest = l.routines[currentroutine].routineLocs[routinestage];
                                }
                                else if (wanderDest != Vector3.Zero) { goTo(wanderDest); }

                                if (wanderDest != Vector3.Zero)
                                {
                                    // To prevent being stuck
                                    if (Vector3.Distance(Vector3.Zero, Client.Self.Velocity) < 0.4 && Vector3.Distance(Vector3.Zero, Client.Self.Velocity) >= 0.1)
                                        stuckTime++;
                                    else { stuckTime = 0; }

                                    if (stuckTime >= 6)
                                    {
                                        Client.Self.Teleport(Client.Network.CurrentSim.Name, wanderDest);
                                        stuckTime = 0;
                                    }
                                }
                            }
                        }
                    }
                    //else { currentroutine = -1; routinestage = -1; }

                    if (s.moveType == 0 && noticelist.Count == 0) // Wander
                    {
                        if (idleTime <= 0)
                        {
                            if (wanderDest == Vector3.Zero)
                            {
                                //MessageBox.Show("Woo");
                                randomizeDestination(out wanderDest);
                            }

                            if (Vector3.Distance(Client.Self.SimPosition, wanderDest) < 10)
                            {
                                idleTime = 10;
                                randomizeDestination(out wanderDest);
                            }
                            else if (wanderDest != Vector3.Zero)
                            {
                                goTo(wanderDest);
                            }
                        }

                        if (wanderDest != Vector3.Zero)
                        {
                            // To prevent being stuck
                            if (Vector3.Distance(Vector3.Zero, Client.Self.Velocity) < 0.4 && Vector3.Distance(Vector3.Zero, Client.Self.Velocity) >= 0.1)
                                stuckTime++;
                            else stuckTime = 0;

                            if (stuckTime >= 6)
                                Client.Self.Teleport(Client.Network.CurrentSim.Name, currentdest);
                            stuckTime = 0;
                        }
                    }
                    else if (s.moveType == 1) // Idle
                    {
                        if (s.idleLoc != Vector3.Zero)
                        {
                            if (Vector3.Distance(Client.Self.SimPosition, s.idleLoc) > 2)
                                goTo(s.idleLoc);
                        }
                    }

                    prevPosition = Client.Self.SimPosition; // Keeps track of where it was last second. Useful for gauging actual speed

                    if (s.botType < 2) // If not an NPC...
                    {
                        UUID tohitch = UUID.Zero;

                        try
                        {
                            Client.Network.CurrentSim.AvatarPositions.ForEach(delegate(KeyValuePair<UUID, Vector3> kvp)
                            {
                                if (Vector3.Distance(Client.Self.SimPosition, kvp.Value) <= 15 && kvp.Key != Client.Self.AgentID)
                                {
                                    if (Vector3.Distance(Client.Self.SimPosition, currentdest) > 5)
                                    {
                                        Client.Self.AutoPilotCancel();
                                        currentdest = Client.Self.SimPosition;
                                        //goTo(Client.Self.SimPosition);
                                    }
                                    idleTime = 10;

                                    Avatar A = Client.Network.CurrentSim.ObjectsAvatars.Find(delegate(Avatar Av) { return Av.ID == kvp.Key; });
                                    if (hasAccess(kvp.Key)) // This will be removed when I'm not worried about other people.
                                    {
                                        if (A.Velocity == Vector3.Zero && Vector3.Distance(Client.Self.SimPosition, kvp.Value) <= 5)
                                        {
                                            if (!greetlist.Contains(kvp.Key)) // If they're not on the list of people we've greeted...
                                            {
                                                greet(kvp.Key);
                                                rememberPerson(kvp.Key, 1);
                                                noticelist.Add(kvp.Key);
                                                if (hhdest != Vector3.Zero) { tohitch = kvp.Key; } // If needs to hitchhike...
                                            }
                                        }
                                        else
                                        {
                                            if (FindClosestAvatar() == kvp.Key)
                                            {
                                                if (!noticelist.Contains(kvp.Key))
                                                {
                                                    noticelist.Add(kvp.Key);
                                                    DoAnimation(Animations.HELLO);
                                                    BotSpeak(getSpeech(64), false);
                                                }
                                                Client.Self.Movement.TurnToward(getPosition(kvp.Key));
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (noticelist.Contains(kvp.Key))
                                    {
                                        noticelist.Remove(kvp.Key);
                                        if (!greetlist.Contains(kvp.Key))
                                        {
                                            DoAnimation(Animations.IMPATIENT);
                                            BotSpeak(getSpeech(65), false);
                                        }
                                    }
                                    if (greetlist.Contains(kvp.Key)) { greetlist.Remove(kvp.Key); }
                                }
                            });
                        }
                        catch { }

                        if (tohitch != UUID.Zero) // Doesn't work when run in the above foreach loop. I imagine it has to do with the delegate doing something thread-related.
                        {
                            System.Threading.Thread.Sleep(1000);

                            if (s.botType == 1) // Tour Guide
                            {
                                // If not giving a tour, ask if they'd like one. If currently giving one, then offer they follow.
                                if (state == 0) { askToTour(tohitch); }
                            }
                            else if (s.botType == 0) // HitchBOT
                            {
                                Avatar A = Client.Network.CurrentSim.ObjectsAvatars.Find(delegate(Avatar Av) { return Av.ID == tohitch; });
                                Client.Self.Movement.TurnToward(hhdest);
                                DoAnimation(Animations.POINT_YOU);
                                //string lala = "";
                                try
                                {
                                    //lala = GetWhere(hhdest);
                                    float distance = 0;
                                    try { distance = (float)(Vector3.Distance(Client.Self.SimPosition, hhdest) / 1609.34); } // Get out of here divide by zero error asklfjasgklja
                                    catch { }
                                    string direction = "";
                                    int angle = (int)(Math.Atan2(hhdest.Y - Client.Self.SimPosition.Y, hhdest.X - Client.Self.SimPosition.X) * 180 / Math.PI);
                                    if (angle < 11 || angle >= 349) { direction = "east"; }
                                    else if (angle < 34 && angle >= 11) { direction = "east-north-east"; }
                                    else if (angle < 56 && angle >= 34) { direction = "north-east"; }
                                    else if (angle < 79 && angle >= 56) { direction = "north-north-east"; }
                                    else if (angle < 101 && angle >= 79) { direction = "north"; }
                                    else if (angle < 112 && angle >= 101) { direction = "north-north-west"; }
                                    else if (angle < 146 && angle >= 112) { direction = "north-west"; }
                                    else if (angle < 168 && angle >= 146) { direction = "west-north-west"; }
                                    else if (angle < 191 && angle >= 168) { direction = "west"; }
                                    else if (angle < 211 && angle >= 191) { direction = "west-south-west"; }
                                    else if (angle < 236 && angle >= 211) { direction = "south-west"; }
                                    else if (angle < 259 && angle >= 236) { direction = "south-south-west"; }
                                    else if (angle < 281 && angle >= 259) { direction = "south"; }
                                    else if (angle < 303 && angle >= 281) { direction = "south-south-east"; }
                                    else if (angle < 326 && angle >= 303) { direction = "south-east"; }
                                    else if (angle < 349 && angle >= 326) { direction = "east-south-east"; }
                                    BotSpeak(String.Format(getSpeech(66), hhdest, distance.ToString(),Vector3.Distance(Client.Self.SimPosition,hhdest),direction));
                                    //BotSpeak("I am on my way to " + hhcity + ", which is about " + distance.ToString() + " miles (" + Vector3.Distance(Client.Self.SimPosition, hhdest) + " meters) going " + direction + ". (" + hhdest + ")", false);
                                }
                                catch { BotSpeak(String.Format(getSpeech(67),hhdest)); } //BotSpeak("I am on my way to " + hhdest + ".", false); }
                                System.Threading.Thread.Sleep(3000);
                                Client.Self.Movement.TurnToward(A.Position);
                                askToFollow(tohitch);
                            }
                        }
                    }
                }
            }
        }
        private void HalfMinute(object source, ElapsedEventArgs e)
        {
            if (s.botType == 0)
            {
                if (following == true && ready == true && Client.Self.SimPosition != Vector3.Zero)
                {
                    try
                    {
                        string here = GetWhere(Client.Self.SimPosition);
                        if (currentcity != here)
                        {
                            currentcity = here;
                            //BotSpeak("Hey, we're in " + here + ".", false);
                            BotSpeak(String.Format(getSpeech(68),here));
                            BotTrivia(here);
                        }
                        currentcity = here;
                    }
                    catch
                    {
                    }
                }
            }
        }

        public void defaultSpeechAdd()
        {
            s.speech.Add(new List<String>());
            int a = s.speech.Count-1;
            s.speech[a].Add("Hello, {0}!"); // 0
            s.speech[a].Add("Would you like me to follow you for a bit?");
            s.speech[a].Add("Right behind you.");
            s.speech[a].Add("Okay, no problem.");
            s.speech[a].Add("...I am following you.");
            s.speech[a].Add("Okay! Sorry, did I bother you?"); // 5
            s.speech[a].Add("Stop what?");
            s.speech[a].Add("Tell me a story");
            s.speech[a].Add("/me nods attentively");
            s.speech[a].Add("/me nods attentively");
            s.speech[a].Add("/me nods attentively"); // 10
            s.speech[a].Add("Is that the end?");
            s.speech[a].Add("Thank you for that!");
            s.speech[a].Add("Oh - sorry!");
            s.speech[a].Add("Okay!");
            s.speech[a].Add("I don't know any stories..."); // 15
            s.speech[a].Add("And that's the end~");
            s.speech[a].Add("This way~");
            s.speech[a].Add("Would you like to join me?");
            s.speech[a].Add("And that is the end of the tour.");
            s.speech[a].Add("Okay!"); // 20
            s.speech[a].Add("Hello!");
            s.speech[a].Add("Would you like to stop the tour?");
            s.speech[a].Add("Okay! I'm at the next stop. Come join me!");
            s.speech[a].Add("Okay! Come talk to me if you change your mind!");
            s.speech[a].Add("Tell me when you would like to proceed."); // 25
            s.speech[a].Add("This is the end of the tour. Anything else you would like to know?");
            s.speech[a].Add("Okay!");
            s.speech[a].Add("...We weren't on a tour, were we?");
            s.speech[a].Add("I tripped a flood detect. I'm sorry, I didn't mean to!");
            s.speech[a].Add("Okay! Attempting to teleport."); // 30
            s.speech[a].Add("/me diligently takes notes.");
            s.speech[a].Add("/me nods and writes down {0}.");
            s.speech[a].Add("/me nods attentively");
            s.speech[a].Add("/me nods attentively");
            s.speech[a].Add("/me nods attentively"); // 35
            s.speech[a].Add("Okay!");
            s.speech[a].Add("...We weren't on a tour, were we?");
            s.speech[a].Add("Sorry, you're not on the access list. I'm only for private use at the moment");
            s.speech[a].Add("Pardon?");
            s.speech[a].Add("Great!"); // 40
            s.speech[a].Add("Okay!");
            s.speech[a].Add("All right.");
            s.speech[a].Add("All right!");
            s.speech[a].Add("Oh, okay...");
            s.speech[a].Add("Arright, no problem."); // 45
            s.speech[a].Add(":("); 
            s.speech[a].Add("Thank you!");
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add(""); // 50
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add(""); // 55
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add("Wanna hear a story?"); // 60
            s.speech[a].Add("Thank you! This is where I wanted to be.");
            s.speech[a].Add("Take care!");
            s.speech[a].Add("...Should I finish the story?");
            s.speech[a].Add("/me waves.");
            s.speech[a].Add("/me sighs."); // 65
            s.speech[a].Add("I am on my way to {0}, which is about {1} miles going {2}.");
            s.speech[a].Add("I am on my way to {0}.");
            s.speech[a].Add("Hey, we're in {0}.");
            s.speech[a].Add("Hello again, {0}!");
            s.speech[a].Add("Would you like a tour?"); // 70
            s.speech[a].Add("Arright. Come talk to me if you change your mind.");
            s.speech[a].Add("No tours available.");
            s.speech[a].Add("I wasn't following you.");
            s.speech[a].Add("Shutting up.");
            s.speech[a].Add("Tweeted!"); // 75
            s.speech[a].Add("That's too long to tweet! It was {0} characters - {1} too many!");
            s.speech[a].Add("The date is {0}. Conditions are {1}, with a low of {2} and a high of {3}.");
            s.speech[a].Add("May I share it?");
            s.speech[a].Add("");
            s.speech[a].Add("I don't believe that's a proper command."); // 80
        }
        public void defaultSpeechAdd(botSettings s)
        {
            s.speech.Add(new List<String>());
            int a = s.speech.Count - 1;
            s.speech[a].Add("Hello, {0}!"); // 0
            s.speech[a].Add("Would you like me to follow you for a bit?");
            s.speech[a].Add("Right behind you.");
            s.speech[a].Add("Okay, no problem.");
            s.speech[a].Add("...I am following you.");
            s.speech[a].Add("Okay! Sorry, did I bother you?"); // 5
            s.speech[a].Add("Stop what?");
            s.speech[a].Add("Tell me a story");
            s.speech[a].Add("/me nods attentively");
            s.speech[a].Add("/me nods attentively");
            s.speech[a].Add("/me nods attentively"); // 10
            s.speech[a].Add("Is that the end?");
            s.speech[a].Add("Thank you for that!");
            s.speech[a].Add("Oh - sorry!");
            s.speech[a].Add("Okay!");
            s.speech[a].Add("I don't know any stories..."); // 15
            s.speech[a].Add("And that's the end~");
            s.speech[a].Add("This way~");
            s.speech[a].Add("Would you like to join me?");
            s.speech[a].Add("And that is the end of the tour.");
            s.speech[a].Add("Okay!"); // 20
            s.speech[a].Add("Hello!");
            s.speech[a].Add("Would you like to stop the tour?");
            s.speech[a].Add("Okay! I'm at the next stop. Come join me!");
            s.speech[a].Add("Okay! Come talk to me if you change your mind!");
            s.speech[a].Add("Tell me when you would like to proceed."); // 25
            s.speech[a].Add("This is the end of the tour. Anything else you would like to know?");
            s.speech[a].Add("Okay!");
            s.speech[a].Add("...We weren't on a tour, were we?");
            s.speech[a].Add("I tripped a flood detect. I'm sorry, I didn't mean to!");
            s.speech[a].Add("Okay! Attempting to teleport."); // 30
            s.speech[a].Add("/me diligently takes notes.");
            s.speech[a].Add("/me nods and writes down {0}.");
            s.speech[a].Add("/me nods attentively");
            s.speech[a].Add("/me nods attentively");
            s.speech[a].Add("/me nods attentively"); // 35
            s.speech[a].Add("Okay!");
            s.speech[a].Add("...We weren't on a tour, were we?");
            s.speech[a].Add("Sorry, you're not on the access list. I'm only for private use at the moment");
            s.speech[a].Add("Pardon?");
            s.speech[a].Add("Great!"); // 40
            s.speech[a].Add("Okay!");
            s.speech[a].Add("All right.");
            s.speech[a].Add("All right!");
            s.speech[a].Add("Oh, okay...");
            s.speech[a].Add("Arright, no problem."); // 45
            s.speech[a].Add(":(");
            s.speech[a].Add("Thank you!");
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add(""); // 50
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add(""); // 55
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add("");
            s.speech[a].Add("Wanna hear a story?"); // 60
            s.speech[a].Add("Thank you! This is where I wanted to be.");
            s.speech[a].Add("Take care!");
            s.speech[a].Add("...Should I finish the story?");
            s.speech[a].Add("/me waves.");
            s.speech[a].Add("/me sighs."); // 65
            s.speech[a].Add("I am on my way to {0}, which is about {1} miles going {2}.");
            s.speech[a].Add("I am on my way to {0}.");
            s.speech[a].Add("Hey, we're in {0}.");
            s.speech[a].Add("Hello again, {0}!");
            s.speech[a].Add("Would you like a tour?"); // 70
            s.speech[a].Add("Arright. Come talk to me if you change your mind.");
            s.speech[a].Add("No tours available.");
            s.speech[a].Add("I wasn't following you.");
            s.speech[a].Add("Shutting up.");
            s.speech[a].Add("Tweeted!"); // 75
            s.speech[a].Add("That's too long to tweet! It was {0} characters - {1} too many!");
            s.speech[a].Add("The date is {0}. Conditions are {1}, with a low of {2} and a high of {3}.");
            s.speech[a].Add("May I share it?");
            s.speech[a].Add("");
            s.speech[a].Add("I don't believe that's a proper command."); // 80
        }

        private static void randomizeDestination()
        {
            Vector3 newdest = new Vector3();
            newdest.X = rnd.Next(255); newdest.Y = rnd.Next(255);
            float butt = 0; // Maturity.
            Client.Network.CurrentSim.TerrainHeightAtPoint((int)newdest.X, (int)newdest.Y, out butt);
            newdest.Z = butt;
            if (butt != 0) { hhdest = newdest; hhcity = GetWhere(hhdest); }
        }
        private static void randomizeDestination(out Vector3 whee)
        {
            whee = Vector3.Zero;
            Vector3 newdest = new Vector3();
            newdest.X = rnd.Next(255); newdest.Y = rnd.Next(255);
            float butt = 0; // Maturity.
            Client.Network.CurrentSim.TerrainHeightAtPoint((int)newdest.X, (int)newdest.Y, out butt);
            newdest.Z = butt;
            if (butt != 0) { whee = newdest; }
        }

        public void loginButton_Click(object sender, EventArgs e)
        {
            if (hasconnection)
            {
                Client.Network.Logout();
                MessageBox.Show("You are now logged out.");
                hasconnection = false;
            }
            else
            {
                if (BotLogin(nameEntry1.Text, nameEntry2.Text, passwordEntry.Text))
                {
                    Client.Self.IM += new EventHandler<InstantMessageEventArgs>(Self_IM);
                    Client.Self.ChatFromSimulator += new EventHandler<ChatEventArgs>(Self_Say);
                    //Client.Self.OnInstantMessage += new AgentManager.InstantMessageCallback(Self_IM); // I think this is obsolete. I fished it up when I was looking for alternatives to the above.
                    hasconnection = true;
                }
            }
        }

        delegate void SetTextCallback(Control ctrl, string text);
        private void SetText(Control ctrl, string text)
        {
            if (ctrl.InvokeRequired)
            {
                ctrl.BeginInvoke(new SetTextCallback(SetText), ctrl, text); // Invokes Control with a delegate that points to SetText as well. So it runs twice, basically.
            }
            else { ctrl.Text = text; }
        }
        
        public static void DoAnimation(UUID a)
        {
            if (Client.Self.SignaledAnimations.ContainsKey(a)) { Client.Self.AnimationStop(a, true); }
            Client.Self.AnimationStart(a, true);
        }
        public void StopAllAnimations()
        {
            Client.Self.SignaledAnimations.ForEach(delegate(KeyValuePair<UUID, int> kvp)
            {
                Client.Self.AnimationStop(kvp.Key, true);
            });
        }

        public static void Tweet(string s)
        {
            using (twit) { twit.UpdateStatus(s); }
        }
        public static void LongTweet(string s)
        {
            // This takes a long string and splits it up.

            string[] foo = s.Split(' ');
            string str = "";
            bool bar = true;
            int at = 0;
            int stag = 0;
            int maxstag = 0;
            double der = (s.Length / (140-8));
            string aaa = Math.Ceiling(der).ToString();
            if (aaa.Length == 1) { aaa = "0" + aaa; }
            string ex = "(" + stag + "/" + aaa + ") ";

            // Dry run to figure out how many substrings are in it, because I'm terrible at math and it varies too much.
            while (bar)
            {
                stag += 1;
                maxstag = stag;
                string s1 = stag.ToString(); if (s1.Length == 1) { s1 = "0" + s1; }
                ex = "(" + s1 + "/" + aaa + ") ";
                str = ex;
                for (int i = at; i < foo.Length; i++)
                {
                    string old = str;
                    if (foo[i].Length > 140 - ex.Length) { continue; } // Just ignore words over 140 characters in length. That would be cray.
                    str += foo[i] + " ";
                    if (str.Length > 140 - ex.Length) { str = old; at = i; break; }
                    if (i == foo.Length - 1) { bar = false; break; }
                }
            }
            at = 0;
            bar = true;
            stag = 0;

            // Fo' reals...
            while (bar)
            {
                stag += 1;
                string s1 = stag.ToString(); if (s1.Length == 1) { s1 = "0" + s1; }
                string s2 = maxstag.ToString(); if (s2.Length == 1) { s2 = "0" + s2; }
                ex = "(" + s1 + "/" + s2 + ") ";
                str = ex;
                for (int i = at; i < foo.Length; i++)
                {
                    string old = str;
                    if (foo[i].Length > 140-ex.Length) { continue; } // Just ignore words over 140 characters in length. That would be cray.
                    str += foo[i] + " ";
                    if (str.Length > 140 - ex.Length) { str = old; at = i; break; }
                    if (i == foo.Length - 1) { bar = false; break; }
                }
                using (twit) { twit.UpdateStatus(str); }
            }
        }

        private static UUID FindClosestAvatar()
        {
            OpenMetaverse.Vector3 mypos = Client.Self.SimPosition;
            OpenMetaverse.Vector3 otherpos = new Vector3();

            UUID mar = new UUID();
            float chec = new float();
            chec = 0;
            Client.Network.CurrentSim.AvatarPositions.ForEach(delegate(KeyValuePair<UUID, Vector3> balls)
            {
                if (balls.Key != Client.Self.AgentID)
                {
                    Client.Network.CurrentSim.AvatarPositions.TryGetValue(balls.Key, out otherpos);
                    if (Vector3.Distance(mypos, otherpos) < chec || chec == 0)
                    {
                        chec = Vector3.Distance(mypos, otherpos);
                        mar = balls.Key;
                    }
                }
            });
            return mar;
        }

        void Network_SimConnected(object sender, SimConnectedEventArgs e)
        {
            SetText(loggedLabel, "Connected");
            ready = true;
            //idleLoc = Client.Self.SimPosition; // Make sure to remove this when we move into more 'real' functionality
            Client.Self.Movement.Mouselook = true;
        }
        void Network_SimDisconnected(object sender, SimDisconnectedEventArgs e)
        {
            SetText(loggedLabel, "Not connected");
            ready = false;
        }
        private static void Avatars_UUIDNameReply(object sender, UUIDNameReplyEventArgs e)
        {
            foreach(KeyValuePair<UUID,string> pair in e.Names) {
                name = pair.Value;
                break;
            }
        }

        public static string GetWoeid(string Zipcode)
        {
            string query = String.Format("http://where.yahooapis.com/v1/places.q('{0}')?appid={1}", Zipcode, "WindowsFormsApplication1.MockingBOT"); //System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            XDocument thisDoc = XDocument.Load(query);
            XNamespace ns = "http://where.yahooapis.com/v1/schema.rng";

            return (from i in thisDoc.Descendants(ns + "place") select i.Element(ns + "woeid").Value).First();
        }

        private static void BotSpeak(string say)
        {
            if (shutup == true) { return; }
            if (flooddetect == 0)
            {
                UUID typing = new UUID("c541c47f-e0c0-058b-ad1a-d6ae3a4584d9");
                Client.Self.AnimationStart(typing, true);
                System.Threading.Thread.Sleep(1000 + say.Split(' ').Length * 100);
                Client.Self.Chat(say, 0, ChatType.Normal);
                Client.Self.AnimationStop(typing, true);
                if (speakingaloud) { Speech.Speak(say); }
            }
            timesmsgd++;
        }
        private static void BotSpeak(string say, bool woo)
        {
            if (shutup == true) { return; }
            if (flooddetect == 0)
            {
                UUID typing = new UUID("c541c47f-e0c0-058b-ad1a-d6ae3a4584d9");
                if (woo == true)
                {
                    Client.Self.AnimationStart(typing, true);
                    System.Threading.Thread.Sleep(1000 + say.Split(' ').Length * 100);
                }
                Client.Self.Chat(say, 0, ChatType.Normal);
                if (woo == true) { Client.Self.AnimationStop(typing, true); }
                if (speakingaloud) { Speech.Speak(say); }
            }
            timesmsgd++;
        }

        private static void BotIM(UUID avi, string say)
        {
            if (flooddetect == 0) { Client.Self.InstantMessage(avi, say); }
            timesmsgd++; 
        }
        private static void BotIM(UUID avi, string say, UUID sid)
        {
            if (flooddetect == 0) { Client.Self.InstantMessage(avi, say, sid); }
            timesmsgd++;
        }
        private static void BotIM(UUID avi, string say, bool woo)
        {
            if (flooddetect == 0) { Client.Self.InstantMessage(avi, say); }
            timesmsgd++;
        }

        private static bool hasAccess(UUID av)
        {
            if (av == Client.Self.AgentID) { return false; }
            if (!s.privateaccess) { return true; }

            if (s.alist.Contains(av)) { return true; }
            else { return false; }
        }
        private static string getName(UUID av)
        {
            name = null;
            int timeout = 0;
            Client.Avatars.RequestAvatarName(av);
            while (name == null) // Temporarily hangs it while the network responds to our request. Wait can go up to ten seconds, but should be nigh-instant.
            {
                Thread.Sleep(1);
                timeout++;
                if (timeout > 10000) {break;}
            }
            return name;
        }

        static void rememberPerson(UUID av, int relate)
        {
            if (!s.mPeople.Contains(av)) { 
                s.mPeople.Add(av); 
                s.mRelationship.Add(0);
                List<int> newlist = new List<int>();
                foreach (textNode t in l.nodes) { newlist.Add(0); }
                s.mLearning.Add(newlist); 
            } // Add to our 'array' of lists.
            int place = s.mPeople.IndexOf(av); // I thiiiiiink this is completely safe? 100%? I get the strange feeling there's something up with it, but w/e.
            s.mRelationship[place] = relate;
        }
        static void rememberTaught(UUID av, int node)
        {
            if (!s.mPeople.Contains(av)) { 
                s.mPeople.Add(av); 
                s.mRelationship.Add(0);
                List<int> newlist = new List<int>();
                foreach (textNode t in l.nodes) { newlist.Add(0); }
                s.mLearning.Add(newlist); 
            } // Add to our 'array' of lists.
            int place = s.mPeople.IndexOf(av); // I thiiiiiink this is completely safe? 100%? I get the strange feeling there's something up with it, but w/e.
            if (s.mLearning[place].Count < l.nodes.Count) { int foo = l.nodes.Count - s.mLearning[place].Count; for (int i = 0; i < foo; i++) { s.mLearning[place].Add(0); } }
            s.mLearning[place][node] = 1; // Person knows this thing now woo // PROBLEMS AAAA
        }

        private static void goTo(Vector3 pos) {
            //if (Vector3.Distance(Client.Self.SimPosition, pos) < 2) { return; }
            //BotSpeak("Going to " + pos.ToString());
            currentdest = pos;
            float woo = new float(); float hoo = new float();
            ulong yay = Helpers.GlobalPosToRegionHandle((float)Client.Self.GlobalPosition.X, (float)Client.Self.GlobalPosition.Y, out woo, out hoo);
            
            uint x = new uint(); uint y = new uint();

            Utils.LongToUInts(yay, out x, out y);

            //Client.Self.AutoPilotLocal((int)pos.X, (int)pos.Y, (int)pos.Z);
            Client.Self.AutoPilot(x+pos.X,y+pos.Y, (int)pos.Z);

            float butt; // Maturity.
            // This just gets the height of land where the leader is standing.
            Client.Network.CurrentSim.TerrainHeightAtPoint((int)pos.X, (int)pos.Y, out butt);

            if (pos.Z > butt + 5 && butt != 0) // This will return if the leader is in the air. Jump height is 5, so this means they're flying, most likely.
            {
                Client.Self.Movement.Fly = true; // If so, fly in pursuit.
            }
            else { Client.Self.Movement.Fly = false; } // And if not, land.

            /*
            if (Vector3.Distance(Client.Self.SimPosition, pos) > 10)
            {
                Client.Self.Movement.AlwaysRun = true;
            }
            else { Client.Self.Movement.AlwaysRun = false; }
            */
        }

        private static string getSpeech(int g)
        {
            string str = "";
            try { str = s.speech[s.currentspeech][g]; }
            catch {
                if (g == 0)       { return "Hello, {0}!"; }
                else if (g == 1)  { return "Would you like me to follow you for a bit?"; }
                else if (g == 2)  { return "Right behind you."; }
                else if (g == 3)  { return "Okay, no problem."; }
                else if (g == 4)  { return "...I am following you."; }
                else if (g == 5)  { return "Okay! Sorry, did I bother you?"; }
                else if (g == 6)  { return "Stop what?"; }
                else if (g == 7)  { return "Tell me a story"; }
                else if (g == 8)  { return "/me nods attentively"; }
                else if (g == 9)  { return "/me nods attentively"; }
                else if (g == 10) { return "/me nods attentively"; }
                else if (g == 11) { return "Is that the end?"; }
                else if (g == 12) { return "Thank you for that!"; }
                else if (g == 13) { return "Oh - sorry!"; }
                else if (g == 14) { return "Okay!"; }
                else if (g == 15) { return "I don't know any stories..."; }
                else if (g == 16) { return "And that's the end~"; }
                else if (g == 17) { return "This way~"; }
                else if (g == 18) { return "Would you like to join me?"; }
                else if (g == 19) { return "And that is the end of the tour."; }
                else if (g == 20) { return "Okay!"; }
                else if (g == 21) { return "Hello!"; }
                else if (g == 22) { return "Would you like to stop the tour?"; }
                else if (g == 23) { return "Okay! I'm at the next stop. Come join me!"; }
                else if (g == 24) { return "Okay! Come talk to me if you change your mind!"; }
                else if (g == 25) { return "Tell me when you would like to proceed."; }
                else if (g == 26) { return "This is the end of the tour. Anything else you would like to know?"; }
                else if (g == 27) { return "Okay!"; }
                else if (g == 28) { return "...We weren't on a tour, were we?"; }
                else if (g == 29) { return "I tripped a flood detect. I'm sorry, I didn't mean to!"; }
                else if (g == 30) { return "Okay! Attempting to teleport."; }
                else if (g == 31) { return "/me diligently takes notes."; }
                else if (g == 32) { return "/me nods and writes down {0}."; }
                else if (g == 33) { return "/me nods attentively"; }
                else if (g == 34) { return "/me nods attentively"; }
                else if (g == 35) { return "/me nods attentively"; }
                else if (g == 36) { return "Okay!"; }
                else if (g == 37) { return "...We weren't on a tour, were we?"; }
                else if (g == 38) { return "Sorry, you're not on the access list. I'm only for private use at the moment"; }
                else if (g == 39) { return "Whaaaaa?"; }
                else { return "";  }
                //str = speech[g]; 
            }
            string[] st = str.Split('|');
            str = st[rnd.Next(st.Length)].Trim();
            return str;
        }

        public static string GetWhere(string lat, string lon)
        {
            string query = String.Format("http://query.yahooapis.com/v1/public/yql?q=select * from geo.placefinder where text = %22{0},{1}%22 and gflags = %22R%22", lat, lon);
            XDocument thisDoc = XDocument.Load(query);
            XNamespace ns = "http://where.yahooapis.com/v1/base.rng";

            return (from i in thisDoc.Descendants("Result") select i.Element("city").Value).First();
        }
        public static string GetWhere(Vector3 woo)
        {
            string myx, myy;
            //myx = (float)81.4217 - (Client.Self.SimPosition.X / (float)76.5098);
            //myy = (float)44.0436 + (Client.Self.SimPosition.Y / (float)76.5098);
            myx = "-" + ((float)81.4217 - (woo.X / (float)76.5098)).ToString();
            myy = ((float)44.0436 + (woo.Y / (float)76.5098)).ToString();

            string query = String.Format("http://query.yahooapis.com/v1/public/yql?q=select * from geo.placefinder where text = %22{0},{1}%22 and gflags = %22R%22", myy, myx);
            XDocument thisDoc = XDocument.Load(query);
            XNamespace ns = "http://where.yahooapis.com/v1/base.rng";

            return (from i in thisDoc.Descendants("Result") select i.Element("city").Value).First();
        }
        public static string GetWoeid(string lat, string lon)
        {
            string query = String.Format("http://query.yahooapis.com/v1/public/yql?q=select * from geo.placefinder where text = %22{0},{1}%22 and gflags = %22R%22", lat, lon);
            XDocument thisDoc = XDocument.Load(query);
            XNamespace ns = "http://where.yahooapis.com/v1/base.rng";

            return (from i in thisDoc.Descendants("Result") select i.Element("woeid").Value).First();
        }

        public static void getWeather(string woeid, out List<string> weather) {
            weather = new List<string>();
            weather.Add(""); weather.Add(""); weather.Add(""); weather.Add("");

            string query = String.Format("http://weather.yahooapis.com/forecastrss?w={0}", woeid);
            XDocument thisDoc = XDocument.Load(query);
            XNamespace ns = "http://xml.weather.yahoo.com/ns/rss/1.0";
            var results = (from i in thisDoc.Descendants(ns + "forecast") select i);
            
            foreach (var thisResult in results)
            {
                weather[0] = (string)thisResult.Attribute("date");
                weather[1] = (string)thisResult.Attribute("text");
                weather[2] = (string)thisResult.Attribute("low");
                weather[3] = (string)thisResult.Attribute("high");
                break;
                //string Woo = String.Format("{0}, it will be {1} with a low of {2} and a high of {3}", thisResult.Attribute("date").Value, thisResult.Attribute("text").Value, thisResult.Attribute("low").Value, thisResult.Attribute("high").Value);
                //MessageBox.Show(Woo);
            }
        }

        public static string toAlphanumeric(string str)
        {
            //string lala = new string("[^a-zA-Z0-9 -]");
            //str = lala.Replace(str, "");
            return str;
        }

        public static int GetNthIndex(string s, char t, int n)
        {
            int count = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == t)
                {
                    count++;
                    if (count == n)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }
        public static void SwapItems(List<string> l, int a, int b) {
            string t = l[a];
            l[a] = l[b];
            l[b] = t;
        }
        public static void SwapItems(List<int> l, int a, int b)
        {
            int t = l[a];
            l[a] = l[b];
            l[b] = t;
        }
        public static void SwapItems(List<Vector3> l, int a, int b)
        {
            Vector3 t = l[a];
            l[a] = l[b];
            l[b] = t;
        }

        public static void LearnString(string str)
        {
            // This takes a string of information and adds it to the database of nodes she can pull up, complete with keywords, etc.
            if (!isStatement(str)) { MessageBox.Show("Not a statement."); return; }
            List<string> keywords = getKeywords(str);

            textNode biscuit = new textNode();
            l.nodes.Add(biscuit);
            biscuit._id = l.nodes.Count;
            biscuit._width = 100;
            biscuit._height = 30;
            biscuit._state = -1;
            biscuit._x = 0;
            biscuit._y = 0;

            for (int i = 0; i < l.bots.Count; i++)
            {
                l.bots[i].allowednodes.Add(biscuit._id);
                for (int ii = 0; ii < l.bots[i].mPeople.Count; ii++) { l.bots[i].mLearning[ii].Add(0); }
            }

            for (int i = 0; i < keywords.Count; i++) { biscuit._subject += keywords[i] + " "; }
            biscuit._data = str;
        }

        public static string toPlainText(string str)
        {
            str = " " + str;
            if (str.Length > 2)
            {
                if (str.StartsWith("*"))
                {
                    str = str.Remove(0, 1);
                    str = str.Trim();
                }
                if (str.Contains("http://")) // URLS
                {
                }

                int i = 0;
                int sanitycheck = 0;
                while (true)
                {
                    sanitycheck++;

                    if (str[i] == '[')
                    {
                        string next = str;
                        int st = i - 1;
                        if (i > 0) { st = i - 1; }
                        else { st = i; }
                        next = next.Remove(0, st); // 'Next' is all text that comes after.

                        try
                        {
                            int woo = next.IndexOf(']'); // The next ] in the line.

                            string bracketed = str.Substring(st, woo + 1);
                            int yay = bracketed.Count(c => c == '['); // For all other brackets in that little pocket.

                            woo = GetNthIndex(next, ']', yay);

                            bracketed = next.Substring(yay+1, woo-(2+yay));
                            //MessageBox.Show(bracketed);

                            str = str.Remove(i, woo); // Cut that shit!

                            if (bracketed.Contains("|"))
                            {
                                bracketed = bracketed.Remove(0, bracketed.IndexOf("|")+1);
                                str = str.Insert(i, bracketed);
                            }
                            else { str = str.Insert(i, bracketed); }

                            i = 0;
                        }
                        catch { str = str.Remove(st); }
                    }
                    else if (str[i] == '{')
                    {
                        string next = str;
                        int st = i - 1;
                        if (i > 0) { st = i - 1; }
                        else { st = i; }
                        next = next.Remove(0, st); // Next is all text that comes after.

                        try
                        {
                            int woo = next.IndexOf('}'); // The next ] in the line.

                            string bracketed = str.Substring(st, woo + 1);
                            int yay = bracketed.Count(c => c == '{'); // For all other brackets in that little pocket.

                            woo = GetNthIndex(next, '}', yay);

                            str = str.Remove(i, woo); // Cut that shit! 
                            i -= 0;
                        }
                        catch { str = str.Remove(st); }
                    }
                    else if (str[i] == '<')
                    {
                        string next = str;
                        if (i > 0) { next = next.Remove(0, i - 1); } // Next is all text that comes after.

                        try
                        {
                            int woo = next.IndexOf('>'); // The next ] in the line.

                            string bracketed = str.Substring(i, woo + 1);
                            int yay = bracketed.Count(c => c == '<'); // For all other brackets in that little pocket.

                            woo = GetNthIndex(next, '>', yay);

                            str = str.Remove(i, woo); // Cut that shit! 
                            i -= 0;
                        }
                        catch { str = str.Remove(i); }
                    }
                    else if (str[i] == '}')
                    {
                        str = str.Remove(0, i);
                    }

                    if (i >= str.Length-1 || i < 0) { break; }
                    i++;
                    //if (sanitycheck > 9999) { MessageBox.Show("Sanity check tripped"); break; }
                }
                str = str.Trim();
                return str;
            }
            return str;
        }

        public static string getWikipedia(string search)
        {
            int trycount = 0;
            string[] result = new string[1];

            while (true)
            {
                trycount++;
                string query = String.Format("http://en.wikipedia.org/w/api.php?format=json&action=query&titles={0}&prop=revisions&rvprop=content", search);

                var json = "";
                using (var webClient = new System.Net.WebClient())
                {
                    json += webClient.DownloadString(query);
                }

                JObject data = JObject.Parse(json);
                //JArray text = (JArray)data["query"]["pages"]["14288"]["revisions"];
                //JObject realtext = (JObject)text[0];
                JToken lala = (JToken)data["query"]["pages"].First.First;
                //MessageBox.Show(lala.ToString());
                JObject text = (JObject)lala["revisions"][0];
                string[] duh = new string[1];
                duh[0] = "\n";
                result = text["*"].ToString().Split(duh, System.StringSplitOptions.None);

                // Some pages, notably Ottawa, are not named as 'City, Province', but just by City. The former often redirects, however.
                // Either way, switching to the redirect just makes sense.
                if (result[0].Contains("#REDIRECT")) 
                {
                    result[0] = result[0].Remove(0,result[0].IndexOf('['));
                    result[0] = result[0].Replace("[","");
                    result[0] = result[0].Replace("]", "");
                    search = result[0]; // This will refrain from moving on, restarting the query, but with the redirect URL instead.
                }
                else { break; }

                if (trycount >= 3) {break;}
            }

            bool woop = false;
            string bloop = "";
            int check = 0;
            while (woop == false)
            {
                check++;
                if (check > 256) { break; }

                bloop = result[rnd.Next(result.Length)];
                int alphan = 0;
                int nonalphan = 0;
                alphan = bloop.Count(c => char.IsLetterOrDigit(c));
                nonalphan = bloop.Count(c => !char.IsLetterOrDigit(c));

                if (bloop.Contains("'''") || (alphan > nonalphan && !bloop.Contains("|"))) // "|" is usually used in code strings.
                {
                    bloop = toPlainText(bloop);
                    string blooplower = bloop.ToLower(); // For easy reading and checking.
                    if (bloop.Length > 100) // If it's fairly long (which tends to filter out the strings which are just code)
                    {
                        if (!blooplower.StartsWith("but") && !blooplower.StartsWith("!")) // If the article does NOT start with a preposition...
                        {
                            woop = true;
                        }
                    }
                }
                else { continue; }
            }

            //MessageBox.Show(bloop);
            //bloop = toPlainText(bloop);
            //MessageBox.Show(bloop);

            return bloop; // result[0].ToString();
        }

        private static void greet(UUID av)
        {
            if (shutup == true) { return; }
            greetlist.Add(av); // Adds them to the list of people we've greeted :D
            getName(av);

            if (s.mPeople.IndexOf(av) != -1)
            {
                if (s.mRelationship[s.mPeople.IndexOf(av)] == 0)
                {
                    BotSpeak(String.Format(getSpeech(0), name)); //"Hello, {0}!"
                }
                else
                {
                    BotSpeak(String.Format(getSpeech(69), name)); // I remember you! :D
                }
            }
            else { BotSpeak(String.Format(getSpeech(0), name)); }
        }
        private static void askToFollow(UUID av)
        {
            if (shutup == true) { return; }
            BotSpeak(getSpeech(1));
            if (waitForAnswer(av,false) == true)
            {
                BotSpeak(getSpeech(2));
                following = true;
                tofollow = av;
            }
            else { BotSpeak(getSpeech(3)); }
            // Query mode. Asked a question; await a response.
        }

        private static bool waitForAnswer(UUID av, bool def)
        {
            int timeout = 0;
            yesno = -1;
            while (true)
            {
                System.Threading.Thread.Sleep(1);
                timeout++;
                if (yesno != -1) {
                    int response = yesno;
                    yesno = -1;
                    if (response == 0) { return false; }
                    else { return true; }
                }
                if (timeout > 15000) { return def; }
            }
            //Client.Self.InstantMessage(av, "Okay!");
            //return true;
        }
        private static bool waitForAnswer(bool def)
        {
            int timeout = 0;
            yesno = -1;
            waiting = true;
            while (true)
            {
                System.Threading.Thread.Sleep(1);
                timeout++;
                if (yesno != -1)
                {
                    int response = yesno;
                    yesno = -1;
                    if (response == 0) { waiting = false; return false; }
                    else { waiting = false; return true; }
                }
                if (timeout > 15000) { waiting = false; return def; }
            }
        }
        private static string waitForAnswer(int w, UUID av, string def)
        {
            string foo = "";
            if (w == 0) { foo = lastresp; }
            else { foo = lastIM; }
            int timeout = 0;
            yesno = -1;
            waiting = true;
            while (true)
            {
                System.Threading.Thread.Sleep(1);
                timeout++;
                if (w == 0)
                {
                    if (lastheard == av && lastresp != foo)
                    {
                        waiting = false;
                        return lastresp;
                    }
                }
                else
                {
                    if (lastIMer == av && lastIM != foo)
                    {
                        waiting = false;
                        return lastIM;
                    }
                }
                if (timeout > 15000) { waiting = false; return def; }
            }
        }

        private static void askQuestion(UUID av, string q, string a)
        {
            if (shutup == true) { return; }
            BotSpeak(q);
            if (waitForAnswer(av,false) == false) 
            {
                BotSpeak(a);
            }
        }
        private static bool askQuestion(int w, bool def, UUID av, string q)
        {
            if (w == 0)
            {
                BotSpeak(q);
                return waitForAnswer(av, def);
            }
            else
            {
                BotIM(av,q);
                return waitForAnswer(av, def);
            }
        }
        private static string askStrQuestion(int w, bool def, UUID av, string q)
        {
            if (w == 0)
            {
                BotSpeak(q);
                return waitForAnswer(w, av, "");
            }
            else
            {
                BotIM(av, q);
                return waitForAnswer(w, av, "");
            }
        }
        private static void askForStory(UUID av)
        {
            currentstory = new botStories();
            l.stories.Add(currentstory);
            currentstory._id = l.stories.Count;
            currentstory._title = "Default";

            listening = true;
            //BotSpeak(getSpeech(7));
            getName(av);
            currentstory._title = name;
        }
        public static void askToTour(UUID av)
        {
            BotSpeak(getSpeech(70), true);
            if (waitForAnswer(false) == true)
            {
                if (l.tours.Count > 0)
                {
                    if (currenttour == null)
                    {
                        int tourgrab = rnd.Next(l.tours.Count);
                        currenttour = l.tours[tourgrab];
                    }
                    tourist = av;
                    goToTour();
                }
                else
                {
                    BotSpeak(getSpeech(72));
                }
            }
            else
            {
                BotSpeak(getSpeech(71));
            }
        }

        private static void tellStory(UUID av)
        {
            if (l.stories.Count > 0)
            {
                if (currentstory == null)
                {
                    int storygrab = rnd.Next(l.stories.Count);
                    currentstory = l.stories[storygrab];
                }
                storytelling = true;
                listener = av;
            }
            else
            {
                BotSpeak(getSpeech(15));
            }
        }

        private static void goToTour()
        {
            if (currenttour.tourLocs.Count > tourStage)
            {
                //BotIM(tourist, "This way~");
                tourLoc = currenttour.tourLocs[tourStage];
                touring = true;
                touristPresent = false;
                tstoryStage = 0;
                BotSpeak(getSpeech(17));
            }
            else {
                //BotIM(tourist, "And that is the end of the tour.");
                touring = false;
                tstoryStage = 0;
                BotSpeak(getSpeech(19));
            }
        }
        private static void askToTeleport(UUID person)
        {
            if (senttele == false) { Client.Self.SendTeleportLure(person, getSpeech(18)); senttele = true; }
        }

        public static Vector3 getPosition(UUID av)
        {
            Avatar A = Client.Network.CurrentSim.ObjectsAvatars.Find(delegate(Avatar Av) { return Av.ID == av; });
            return A.Position;
        }

        public static void waitForTourist(UUID t)
        {
            if (touristPresent == false)
            {
                Vector3 pos2 = new Vector3();
                //bool isonline = true;
                pos2 = getPosition(t);

                if (Vector3.Distance(Client.Self.SimPosition, pos2) <= 5) // Person is there.
                {
                    //BotIM(t, "Hello!");
                    touristPresent = true;
                    if (tourWait > 5) { BotSpeak(getSpeech(21)); }
                    else { BotSpeak(getSpeech(20)); }
                }

                if (tourWait == 20)
                {
                    askToTeleport(t);
                }

                if (tourWait == 30)
                {
                    BotIM(t, getSpeech(22));
                    if (waitForAnswer(t,true) == false) {
                        tourWait = 0;
                        BotIM(t, getSpeech(23));
                    }
                    else
                    {
                        BotIM(t, getSpeech(24));
                        touring = false;
                    }
                }
            }
        }

        public static void BotTrivia(string woo) 
        {
            string trivia = getWikipedia(woo);
            if (trivia.Length > 0) // Sanity check
            {
                string flavatext = "";
                int ran = rnd.Next(5);
                if (ran == 0) { flavatext = "Here's some trivia for ya: "; }
                else if (ran == 1) { flavatext = "Here's a fun fact! "; }
                else if (ran == 2) { flavatext = "Wanna hear some trivia? Too bad. "; }
                else if (ran == 3) { flavatext = "Let me look up the Wikipedia on this place! "; }
                else if (ran == 4) { flavatext = "Excuse me while I reach into the Internets to tell you more of this magical land. "; }
                BotSpeak(flavatext + trivia);
            }
        }

        public static void handleDialogue(UUID av, string foo)
        {
            string[] bar = foo.Split(' '); // For detecting lone words by their lonely lonesome (so 'you' doesn't trigger the check for 'yo', for example).

            if (foo.Contains("stop following") || foo.Contains("don't follow") || foo.Contains("go away"))
            {
                if (following == true)
                {
                    following = false;
                    BotSpeak(getSpeech(5));
                }
                else
                {
                    BotSpeak(getSpeech(73));
                } 
            }
            else if (bar.Contains("follow") || foo.Contains("come with")) // To-do: 'Go with X' to make it follow another person.
            {
                if (following == false)
                {
                    following = true; tofollow = av;
                    BotSpeak(getSpeech(2));
                }
                else
                {
                    BotSpeak(getSpeech(4));
                }
            }

            if (foo.Contains("shut up") || foo.Contains("be quiet") || foo.Contains("don't talk"))
            {
                BotSpeak(getSpeech(74));
                shutup = true;
            }
            if (foo.Contains("you can talk") || foo.Contains("you can speak"))
            {
                shutup = false;
                BotSpeak(getSpeech(41));
            }


            if (foo.Contains("fuck you") || foo.Contains("screw you") || foo.Contains("i hate you") || foo.Contains("you suck")) 
            { 
                BotSpeak(getSpeech(45)); 
            }

            if (bar.Contains("hi") || bar.Contains("hey") || bar.Contains("hello") || bar.Contains("yo")) 
            {
                string lol = "Hi.";
                int r = rnd.Next(10);
                if (r == 0) { lol = "Hey."; }
                if (r == 1) { lol = "Hello."; }
                if (r == 2) { lol = "Yo."; }
                if (r == 3) { lol = "Hi."; }
                if (r == 4) { lol = "'Sup."; }
                if (r == 5) { lol = "Hey there."; }
                if (r == 6) { lol = "Hello-hello."; }
                if (r == 7) { lol = "Yo-yo."; }
                if (r == 8) { lol = "Howdy."; }
                if (r == 9) { lol = "Hallo."; }
                BotSpeak(lol); 
            }
        }

        static bool BotLogin(string firstname, string lastname, string password)
        {
            if (Client.Network.Login(firstname, lastname, password, "FirstBot", "1.0"))
            {
                //MessageBox.Show("I have successfully logged into Second Life!\nThe Message of the day is " + Client.Network.LoginMessage);
                //Client.Appearance.RequestSetAppearance(true);
                return true;
            }
            else
            {
                MessageBox.Show("I was unable to login to Second Life. The Login Server said: " + Client.Network.LoginMessage);
                return false;
            }
        }

        public static void ChangeState(int s)
        {
            state = s;
            MessageBox.Show("State is now " + state.ToString());
        }

        static textNode SearchNode(string check)
        {
            Dictionary<textNode, int> searchquery = new Dictionary<textNode, int>(); // Search for a node
            string chec = check.ToLower();
            chec = new string(chec.Where(c => !char.IsPunctuation(c)).ToArray());

            foreach (textNode t in l.nodes)
            {
                if (t._state != state && t._state != -1) { continue; }

                int pri = 0;
                if (!string.IsNullOrEmpty(t._subject))
                {
                    string str = t._subject.ToLower();
                    foreach (string word in chec.Split(' ')) // For every word...
                    {
                        if (str.Contains(word))
                        {
                            pri += 10;
                        }
                    }
                    if (pri != 0) { searchquery.Add(t, pri); }
                }
            }

            if (searchquery.Count <= 0)
            {
                foreach (textNode t in l.nodes)
                {
                    if (t._state != state && t._state != -1) { continue; }

                    int pri = 0;
                    if (!string.IsNullOrEmpty(t._data))
                    {
                        string str = t._data.ToLower();
                        foreach (string word in chec.Split(' ')) // For every word...
                        {
                            if (str.Contains(word))
                            {
                                pri += 10;
                            }
                        }
                        if (pri != 0) { searchquery.Add(t, pri); }
                    }
                }

            }

            int lala = -1;
            textNode dnode = null;

            foreach (KeyValuePair<textNode, int> pair in searchquery)
            {
                if (pair.Value > lala) { dnode = pair.Key; }
            }

            return dnode;
        }
        static textNode SearchNode(List<string> chec)
        {
            Dictionary<textNode, int> searchquery = new Dictionary<textNode, int>(); // Search for a node

            foreach (textNode t in l.nodes)
            {
                if (t._state != state && t._state != -1) { continue; }

                int pri = 0;
                if (!string.IsNullOrEmpty(t._subject))
                {
                    string str = t._subject.ToLower();
                    foreach (string word in chec) // For every word...
                    {
                        if (str.Contains(word))
                        {
                            pri += 10;
                        }
                    }
                    if (pri != 0) { searchquery.Add(t, pri); }
                }
            }

            if (searchquery.Count <= 0)
            {
                foreach (textNode t in l.nodes)
                {
                    if (t._state != state && t._state != -1) { continue; }

                    int pri = 0;
                    if (!string.IsNullOrEmpty(t._data))
                    {
                        List<string> str = getKeywords(t._data.ToLower());
                        foreach (string word in chec) // For every word...
                        {
                            if (str.Contains(word))
                            {
                                pri += 10;
                            }
                        }
                        if (pri != 0) { searchquery.Add(t, pri); }
                    }
                }
            }

            int lala = -1;
            textNode dnode = null;

            foreach (KeyValuePair<textNode, int> pair in searchquery)
            {
                if (pair.Value > lala) { dnode = pair.Key; }
            }

            return dnode;
        }
        static textNode SearchNode(UUID a, List<string> chec)
        {
            Dictionary<textNode, int> searchquery = new Dictionary<textNode, int>(); // Search for a node
            string keywordsused = "";

            foreach (textNode t in l.nodes)
            {
                if (t._state != state && t._state != -1) { continue; }

                int pri = 0;
                if (!string.IsNullOrEmpty(t._subject))
                {
                    string str = t._subject.ToLower();
                    string[] strs = str.Split(' ');
                    foreach (string word in chec) // For every word...
                    {
                        for (int i = 0; i < strs.Length; i++)
                        {
                            if (strs[i] == word.ToLower())
                            {
                                pri += 10;
                                keywordsused += word + " ";
                            }
                            else
                            {
                                if (strs[i].Contains("^"))
                                {
                                    List<string> andchec = strs[i].Split('^').ToList();
                                    int req = andchec.Count;
                                    string wordsused = "";
                                    foreach (string w in chec) { if (andchec.Contains(w)) { req -= 1; wordsused += "" + w + " "; andchec.Remove(w); } }
                                    if (req <= 0) { pri += 10; keywordsused += wordsused; }
                                }
                                if (strs[i].Contains("||"))
                                {

                                }
                            }
                        }
                    }
                    if (pri > 0) { searchquery.Add(t, pri); }
                }
            }

            if (searchquery.Count <= 0)
            {
                int i = 0;
                foreach (textNode t in l.nodes)
                {
                    if (t._state != state && t._state != -1) { continue; }

                    int pri = 0;
                    if (!string.IsNullOrEmpty(t._data))
                    {
                        List<string> str = getKeywords(t._data.ToLower());
                        foreach (string word in chec) // For every word...
                        {
                            if (str.Contains(word))
                            {
                                pri += 10;
                            }
                        }
                        if (s.mPeople.IndexOf(a) != -1)
                        {
                            int foo = s.mLearning[s.mPeople.IndexOf(a)][i];
                            if (foo > 0) { pri -= 5; } // They know it; less likely to make it the one to show.
                        }
                        if (pri != 0) { searchquery.Add(t, pri); }
                    }
                    i++;
                }
            }

            int lala = -1;
            textNode dnode = null;

            foreach (KeyValuePair<textNode, int> pair in searchquery)
            {
                if (pair.Value > lala) { dnode = pair.Key; }
            }

            return dnode;
        }
        static textNode SearchNode(botSettings b, UUID a, List<string> chec)
        {
            Dictionary<textNode, int> searchquery = new Dictionary<textNode, int>(); // Search for a node
            string keywordsused = "";

            foreach (textNode t in l.nodes)
            {
                if ((t._state != state && t._state != -1) || !b.allowednodes.Contains(t._id)) { continue; }

                int pri = 0;
                if (!string.IsNullOrEmpty(t._subject))
                {
                    string str = Simplify(t._subject.ToLower());
                    string[] strs = str.Split(' ');
                    foreach (string word in chec) // For every word...
                    {
                        for (int i = 0; i < strs.Length; i++)
                        {
                            if (strs[i] == word.ToLower())
                            {
                                pri += 10;
                                keywordsused += word + " ";
                            }
                            else
                            {
                                if (strs[i].Contains("^"))
                                {
                                    List<string> andchec = strs[i].Split('^').ToList();
                                    int req = andchec.Count;
                                    string wordsused = "";
                                    foreach (string w in chec) { if (andchec.Contains(w)) { req -= 1; wordsused += "" + w + " "; andchec.Remove(w); } }
                                    if (req <= 0) { pri += 10; keywordsused += wordsused; }
                                }
                                if (strs[i].Contains("||"))
                                {

                                }
                            }
                        }
                    }
                    if (pri > 0) { searchquery.Add(t, pri); }
                }
            }
            /*
            if (searchquery.Count <= 0)
            {
                int i = 0;
                foreach (textNode t in l.nodes)
                {
                    if ((t._state != state && t._state != -1) || !b.allowednodes.Contains(t._id)) { continue; }

                    int pri = 0;
                    if (!string.IsNullOrEmpty(t._data))
                    {
                        List<string> str = getKeywords(t._data.ToLower());
                        foreach (string word in chec) // For every word...
                        {
                            if (str.Contains(word))
                            {
                                pri += 10;
                            }
                        }
                        if (s.mPeople.IndexOf(a) != -1)
                        {
                            int foo = s.mLearning[s.mPeople.IndexOf(a)][i];
                            if (foo > 0) { pri -= 5; } // They know it; less likely to make it the one to show.
                        }
                        if (pri != 0) { searchquery.Add(t, pri); }
                    }
                    i++;
                }
            }
            */
            int lala = -1;
            textNode dnode = null;

            foreach (KeyValuePair<textNode, int> pair in searchquery)
            {
                if (pair.Value > lala) { dnode = pair.Key; }
            }

            return dnode;
        }

        static string ReadNode(string data)
        {
            string newdata = "";
            if (data.Length == 0) { return data; } // Sanity check
            
            string[] words = data.Split(' ');
            

            foreach (string word in words)
            {/*
                if (word.EndsWith(")"))
                {
                    // Check for function woo
                    string mc = word;
                    mc = mc.Remove(mc.IndexOf("(")); // Remove arguments

                    string argms = word;
                    argms = argms.Remove(0, argms.IndexOf("(")+1);
                    argms = argms.Remove(argms.Length-1);
                    
                        string[] woo = argms.Split(',');

                        Object[] args = new Object[woo.Length];

                        int i = 0;
                        Type q = typeof(Program);

                        foreach (string god in woo)
                        {
                            if (!String.IsNullOrWhiteSpace(god))
                            {
                                string ar = god;
                                ar = ar.Remove(0, 1);
                                if (god.StartsWith("b")) { args[i] = Boolean.Parse(ar); } // Booleans
                                else if (god.StartsWith("i"))
                                {
                                    int floop = new int(); int.TryParse(ar, out floop);
                                    args[i] = floop;
                                }
                                else if (god.StartsWith("f"))
                                {
                                    float floop = new float(); float.TryParse(ar, out floop);
                                    args[i] = floop;
                                }
                                else if (god.StartsWith("U"))
                                {
                                    //UUID floop = new UUID(); UUID.TryParse(ar, out floop);
                                    args[i] = q.GetField(ar);
                                    MessageBox.Show(args[i].ToString());
                                }
                                else if (god.StartsWith("s"))
                                {
                                    args[i] = ar;
                                }
                            }
                            i++;
                        }
                    
                    //Type t = typeof(AgentManager);
                    //t.InvokeMember(mc,BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,null,Client.Self,args);
                    //Type t = typeof(MockingBOT);
                    //t.InvokeMember(mc, BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance, null, null, args);
                }                
                else
                {
                    newdata += word + " ";
                }
                */
                newdata += word + " ";
            }

            //newdata = String.Join(" ",words);
            return newdata;
        }

        static string getWordType(string w)
        {
            w = new string(w.Where(c => !char.IsPunctuation(c)).ToArray());

            // First, some quick fixes, because the dictionary API I use is kind of insane.
            if (w == "are") { return "Verb"; }
            if (w == "upon") { return "Preposition"; }
            if (w == "spoke") { return "Verb"; }
            if (w == "I") { return "Pronoun"; }

            if (s.wordsKnown.Contains(w))
            {
                return s.wordTypes[s.wordsKnown.IndexOf(w)];
            }
            else
            {
                // Okay, things about this: Dictionary APIs are really hard to find. A lot of them impose limits.
                // To circumvent this, words are remembered by the bot, to reduce this being used.
                try
                {
                    string query = String.Format("http://www.google.com/dictionary/json?callback=dict_api.callbacks.id100&q={0}&sl=en&tl=en&restrict=pr%2Cde&client=te", w);

                    var json = "";
                    using (var webClient = new System.Net.WebClient())
                    {
                        json += webClient.DownloadString(query);
                    }
                    json = json.Remove(0, json.IndexOf('(') + 1);
                    json = json.Remove(json.LastIndexOf('}') + 1);
                    json = json.Replace("\\x", ""); // It hates \x for some reason.
                    JObject data = JObject.Parse(json);
                    //MessageBox.Show(json.ToString());
                    JToken t = (JToken)data["primaries"].First["terms"].First["labels"].First["text"];

                    // Add to words we know.
                    s.wordsKnown.Add(w);
                    s.wordTypes.Add(t.ToString());

                    return t.ToString();
                }
                catch
                {
                    s.wordsKnown.Add(w);
                    s.wordTypes.Add("Noun"); // If it's not known, PROBABLY a noun.

                    return "Noun";
                } 
            }
        }

        static List<string> getKeywords(string str)
        {
            str = str.ToLower();
            List<string> l = new List<string>();
            string[] generic = new string[] { "so","your","i", "a", "about", "an", "and", "are", "as", "at", "be", "by", "com", "de", "en", "for", "from", "in", "is", "it",
                "la", "of", "on", "or", "that", "the", "this", "to", "was", "will", "with", "und", "the", "www" };

            string[] s = str.Split(' ');
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i].Length > 0)
                {
                    s[i] = new string(s[i].Where(c => !char.IsPunctuation(c)).ToArray()); // No punctuation!
                    string foo = getWordType(s[i]);
                    if (!generic.Contains(s[i]) && (foo == "Noun" || foo == "Verb" || foo == "Adjective" || foo == "Adverb")) { l.Add(s[i]); }
                }
            }

            return l;
        }
        static List<string> getSubject(string str)
        {
            List<string> l = new List<string>();
            List<string> whee = getKeywords(str);
            foreach (string w in whee)
            {
                if (getWordType(w) == "Noun") { l.Add(w); }
            }
            return l;
        }

        static bool isQuestion(string str)
        {
            str = str.ToLower();
            if (str.EndsWith("?")) { return true; } // No brainer.
            string[] s = str.Split(' ');
            int likelihood = 0;

            for (int i = 0; i < s.Length; i++)
            {
                if (i < 32)
                {
                    if (i == 0 || (s[0].Last() == ',' && i == 1)) // If the first word, or the second if the first word is something like 'so,', 'hey,', etc...
                    {
                        if (s[i] == "do") { likelihood += 6; } // Can be a statement. 'Do the best you can'.
                        if (s[i] == "would") { likelihood += 10; }
                        if (s[i] == "can") { likelihood += 10; }
                        if (s[i] == "is") { likelihood += 10; }
                        if (s[i] == "will") { likelihood += 10; }
                        if (s[i] == "are") { likelihood += 10; }
                        if (s[i] == "may") { likelihood += 4; } // Can be a statement. 'May the force be with you'.
                        // This one is higher because it's likely voiced as a command, which trips up later parts of the algorithm that 
                        // rule out commands, statements, etc. While not a question, it deserves a response.
                        if (s.Length > i+1) { if (s[i] == "I" && s[i + 1] == "would") { likelihood += 30; } }
                        if (s.Length > i+2) { if (s[i] == "tell" && s[i + 1] == "me") { likelihood += 30; } } // Likewise.
                    }

                    if (s[i] == "who" || s[i] == "when" || s[i] == "what" || s[i] == "why" || s[i] == "where" || s[i] == "how")
                    {
                        likelihood += 10;
                    }
                    if (s[i] == Client.Self.FirstName.ToLower()) {
                        if (s.Length > i+1) { if (s[i + 1] != "is" && s[i + 1] != "can") { likelihood += 5; } }
                        else {likelihood += 4;}
                    }
                }
            }
            if (str.EndsWith(".") || str.EndsWith(",") || str.EndsWith("!")) { likelihood -= 20; } // While netspeakers may not include a question mark, another form of punctuation is more obviously not a question.

            if (likelihood > 5) { return true; }

            return false;
        }
        static bool isStatement(string str)
        {
            // Checks if the string is a truth-functional statement.
            if (isQuestion(str) == true) { return false; } // Questions a'int statements.
            return true;
        }
        static List<string> getCompoundSentences(string str)
        {
            List<string> c = new List<string>();
            string[] whee = str.Split(' ');
            int last = 0;
            for (int i = 0; i < whee.Length; i++)
            {
                int cut = -1;
                int skip = 0;
                if (i < whee.Length - 1)
                {
                    if (conjunctions.Contains(whee[i + 1]))
                    {
                        if (whee[i].EndsWith(",")) { cut = i + 1; skip = 1; } // Skip one because it needs to exclude the conjunction.
                        // Sometimes you can have a conjunction without a comma though...
                    }
                    //else if (whee[i].EndsWith(";")) { cut = i + 1; }
                    else if (char.IsPunctuation(whee[i].Last()) && whee[i].Last() != ',') { cut = i + 1; }
                    else if (whee[i].EndsWith(",")) {
                        // There's a bajillion ways to use commas. Filter it out here.
                        bool yay = false;
                        
                        // 'X, X, and X' / 'X, X, but X' / etc.
                        for (int ii = i; ii < whee.Length-1; ii++) { if (conjunctions.Contains(whee[ii + 1]) && whee[ii].EndsWith(",")) { yay = true; } }

                        if (yay == true) { cut = i + 1; }
                    } 
                }

                if (cut != -1)
                {
                    char punc = ' ';
                    for (int ii = last; ii < whee.Length; ii++) {
                        if (char.IsPunctuation(whee[ii].Last()) && whee[ii].Last() != ',') 
                        { punc = whee[ii].Last(); break; } 
                    } 

                    string w = "";
                    for (int ii = last; ii < cut; ii++) { w += whee[ii] + " "; }
                    w = w.Trim();
                    if (char.IsPunctuation(w.Last())) { w = w.Remove(w.Length-1); }
                    w += punc; // Adds the punctuation from the end of the sentence.

                    c.Add(w);
                    last = i+1+skip;
                }

                // End. Finish up by adding the remainder.
                if (i == whee.Length - 1) { string w = ""; for (int ii = last; ii <= i; ii++) { w += whee[ii] + " "; } w = w.Trim(); c.Add(w); }
            }
            return c;
        }

        static string Simplify(string str)
        {
            // This returns a string that's more machine-readable.
            string s = str.ToLower();

            List<string> contraction = new List<string> { "i'm", "i'll", "i'd", "i've", "you're", "you'll", "you'd", "you've", "he's", "he'll", "he'd", "she's", "she'll", "she'd",
                "it's", "it'll", "it'd", "we're","we'll","we'd","we've","they're","they'll","they'd","they've","that's","what'll","that'd","who's","who'll","who'd","what's","what'll",
                "what'd","what's","where's","where'll","where'd","when's","when'll","when'd","why's","why'll","why'd","how's","how'll","how'd","isn't","aren't","wasn't","weren't",
                "haven't","hasn't","hadn't","won't","wouldn't","don't","doesn't","didn't","can't","couldn't","shouldn't","mightn't","musn't","would've","should've","could've","might've","must've",
                "wanna","ya" };

            List<string> simplified = new List<string> { "i am", "i will", "i would", "i have", "you are", "you will", "you would", "you have", "he is", "he will", "he would", "she is",
                "she will", "she would", "it is", "it will", "it would", "we are", "we will", "we would", "we have", "they are", "they will", "they would", "they have", "that is", "what will",
                "that would", "who is", "who will", "who would", "what is", "what will", "what would", "what is", "where is", "where will", "where did", "when is", "when will", 
                "when did", "why is", "why will", "why did", "how is", "how will", "how did", "is not", "are not", "was not", "were not", "have not", "has not", "had not", "will not", 
                "would not", "do not", "does not", "did not", "can not", "could not", "should not","might not", "must not", "would have", "should have", "could have", "might have", "must have",
                "want to","yes" };

            List<string> netspeak = new List<string> { "u", "r", "y", "afaik","afk","asl","brb","btw","bbiab","bbl","cu","cya","ffs","ftw","fu","fyi","gg","gj","gtfo","gtg","g2g","gr8",
                "idc","idk","iirc","imo","imho","irl","iwsn","jk","j/k","jfgi","k","kk","kthnx","kthnxbai","kthxbye","lmk","ne1","np","nsfw","nm","o rly","oic","omg","omfg","pov","ppl",
                "rl","stfu","tbh","tmi","ttyl","ty","wb","w/e","w8","wtf","yw","ur","bby","d2f","dtf" };

            List<string> english = new List<string> { "you", "are", "why","as far as i know","away from keyboard","age sex location","be right back","by the way","be back in a bit",
                "be back later","see you","see you","for fuck's sake","for the win","fuck you","for your information","good going","good job","get the fuck out","got to go","got to go",
                "great","i don't care","i don't know","if i recall correctly","in my opinion","in my honest opinion","in real life","i want sex now","just kidding","just kidding",
                "just fucking google it","okay","okay","okay thanks","okay thanks bye","okay thanks bye","let me know","anyone","no problem","not safe for work","nevermind","oh really",
                "oh i see","oh my god","oh my fucking god","point of view","people","real life","shut the fuck up","to be honest","too much information","talk to you later","thank you","welcome back",
                "whatever","wait","what the fuck","you're welcome","your","baby","down to fuck","down to fuck"};

            if (contraction.Count != simplified.Count) { MessageBox.Show("Warning"); }
            if (netspeak.Count != english.Count) { MessageBox.Show("Warning"); }

            string[] f = s.Split(' ');
            s = "";
            for (int i = 0; i < f.Length; i++ )
            {
                string foo = f[i];
                string bar = new string(foo.Where(c => !char.IsPunctuation(c)).ToArray());

                if (contraction.Contains(foo)) { foo = foo.Replace(foo, simplified[contraction.IndexOf(foo)]); }
                if (netspeak.Contains(foo)) { foo = foo.Replace(foo, english[netspeak.IndexOf(foo)]); }

                if (contraction.Contains(bar)) { foo = foo.Replace(bar, simplified[contraction.IndexOf(bar)]); }
                if (netspeak.Contains(bar)) { foo = foo.Replace(bar, english[netspeak.IndexOf(bar)]); }

                string[] aaa = foo.Split(' ');
                foo = "";
                for (int ii = 0; ii < aaa.Length; ii++) { if (aaa[ii] == "i") { aaa[ii] = "I"; } foo += aaa[ii] + " "; }
                foo = foo.Trim();

                s += foo;
                if (i < f.Length - 1) { s += " "; }
            }

            return s;
        }

        static void Self_Say(object sender, ChatEventArgs e)
        {
            if (e.OwnerID != Client.Self.AgentID && e.SourceType == ChatSourceType.Agent)
            {
                if (e.Type == ChatType.Normal)
                {
                    lastresp = e.Message;
                    lastheard = e.OwnerID;

                    OpenMetaverse.Vector3 pos = Client.Self.SimPosition;
                    OpenMetaverse.Vector3 pos2 = e.Position;

                    Avatar A = Client.Network.CurrentSim.ObjectsAvatars.Find(delegate(Avatar Av) { return Av.ID == e.SourceID; });
                    pos2 = A.Position;

                    string say = "";
                    List<string> res = getCompoundSentences(Simplify(e.Message.ToLower()));

                    foreach (string msg in res)
                    {
                        //string msg = e.Message;
                        //msg = msg.ToLower();

                        if (msg.StartsWith("!"))
                        {
                            if (msg.StartsWith("!tweet")) {
                                string foo;
                                foo = msg.Remove(0,6);
                                foo = foo.Trim();
                                string tw = e.FromName + " says: " + foo;
                                if (tw.Length < 140)
                                {
                                    Tweet(tw);
                                    BotSpeak(getSpeech(75));
                                }
                                else { BotSpeak(String.Format(getSpeech(76),tw.Length,(tw.Length-140)));} //BotSpeak("That's too long to tweet! It was " + tw.Length + " characters - " + (tw.Length-140) + " characters too many!"); }
                            }
                            if (msg.StartsWith("!longtweet"))
                            {
                                string foo;
                                foo = msg.Remove(0, 10);
                                foo = foo.Trim();
                                string tw = e.FromName + " says: " + foo;
                                if (tw.Length < 140)
                                {
                                    LongTweet(tw);
                                    BotSpeak(getSpeech(75));
                                }
                            }
                            else if (msg == "!come")
                            {
                                float butt;
                                Client.Network.CurrentSim.TerrainHeightAtPoint((int)pos2.X, (int)pos2.Y, out butt);

                                if (pos2.Z > butt + 6 && butt != 0) { Client.Self.Movement.Fly = true; }
                                else { Client.Self.Movement.Fly = false; }
                                goTo(pos2);
                            }
                            else if (msg == "!follow")
                            {
                                if (following == false)
                                {
                                    following = true; tofollow = e.OwnerID;
                                    BotSpeak(getSpeech(2));
                                }
                                else
                                {
                                    BotSpeak(getSpeech(4));
                                }
                            }
                            else if (msg == "!stop")
                            {
                                if (following == true)
                                {
                                    BotSpeak(getSpeech(5));
                                    following = false;
                                }
                                else
                                {
                                    BotSpeak(getSpeech(6));
                                }
                            }
                            else if (msg == "!teleport")
                            {
                                Client.Self.SendTeleportLure(e.SourceID);
                                BotSpeak(getSpeech(30));
                            }
                            else if (msg == "!where")
                            {
                                BotSpeak("I am at " + Client.Network.CurrentSim.Name + ". My position is " + pos.X + ", " + pos.Y);
                            }
                            else if (msg == "!point")
                            {
                                Client.Self.AnimationStart(Animations.POINT_YOU, true);
                            }
                            else if (msg == "!whome")
                            {
                                BotSpeak("You are " + e.FromName + ", at position " + e.Position + " in " + e.Simulator.Name);
                            }
                            else if (msg == "!look")
                            {
                                Client.Self.Movement.Camera.LookDirection(pos2);
                            }
                            else if (msg == "!whoclosest")
                            {
                                UUID closest = FindClosestAvatar();
                                string balls = closest.ToString(); // Maturity.
                                BotSpeak("That would be " + balls);
                            }
                            else if (msg == "!tour")
                            {
                                if (l.tours.Count > 0)
                                {
                                    if (currenttour == null)
                                    {
                                        int tourgrab = rnd.Next(l.tours.Count);
                                        currenttour = l.tours[tourgrab];
                                    }
                                    tourist = e.OwnerID;
                                    goToTour();
                                }
                                else
                                {
                                    BotSpeak(getSpeech(72));
                                }
                            }
                            else if (msg == "!setidle")
                            {
                                s.idleLoc = pos2;
                            }
                            else if (msg == "!learntour")
                            {
                                currenttour = new botTours();
                                l.tours.Add(currenttour);
                                currenttour._id = l.tours.Count;
                                currenttour.enabled = false;

                                learningtour = true;
                                BotSpeak(getSpeech(31));
                            }
                            else if (msg == "!tourhere")
                            {
                                if (learningtour)
                                {
                                    tourStage++;
                                    List<string> woo = new List<string>();
                                    currenttour.tourInfo.Add(woo);
                                    currenttour.tourLocs.Add(pos2);
                                    BotSpeak(String.Format(getSpeech(32), pos2.X + ", " + pos2.Y + ", " + pos2.Z));
                                }
                            }
                            else if (msg == "!finishtour")
                            {
                                if (learningtour == true)
                                {
                                    learningtour = false;
                                    currenttour = null;
                                    BotSpeak(getSpeech(36));
                                }
                                else
                                {
                                    BotSpeak(getSpeech(37));
                                }
                            }
                            else if (msg == "!stoptour")
                            {
                                if (touring == true)
                                {
                                    touring = false;
                                    currenttour = null;
                                    BotSpeak(getSpeech(27));
                                }
                                else
                                {
                                    BotSpeak(getSpeech(28));
                                }
                            }
                            else if (msg == "!learnroutine")
                            {
                                botRoutines foo = new botRoutines();
                                l.routines.Add(foo);
                                foo._id = l.routines.Count;
                                currentroutine = foo._id-1;

                                l.routines[currentroutine].routineLocs.Add(Vector3.Zero);
                                l.routines[currentroutine].routinePauses.Add(0);
                                l.routines[currentroutine].routineSpeech.Add("");
                                l.routines[currentroutine].routineAnimations.Add(UUID.Zero);
                                routinestage = 0;

                                learningroutine = true;
                                BotSpeak(getSpeech(31));
                            }
                            else if (msg == "!routehere")
                            {
                                if (learningroutine)
                                {
                                    l.routines[currentroutine].routineLocs[routinestage] = pos2;
                                    BotSpeak(String.Format(getSpeech(32), pos2.X + ", " + pos2.Y + ", " + pos2.Z));
                                }
                            }
                            else if (msg == "!routenextstage")
                            {
                                if (learningroutine)
                                {
                                    l.routines[currentroutine].routineLocs.Add(pos2);
                                    l.routines[currentroutine].routinePauses.Add(0);
                                    l.routines[currentroutine].routineSpeech.Add("");
                                    l.routines[currentroutine].routineAnimations.Add(UUID.Zero);
                                    routinestage++;
                                    BotSpeak("Now taking notes for stage " + routinestage.ToString());
                                }
                            }
                            else if (msg == "!routinecheck")
                            {
                                if (learningroutine)
                                {
                                    BotSpeak("Now taking notes for stage " + routinestage.ToString() + " in routine " + currentroutine.ToString() + ".");
                                }
                                else
                                {
                                    if (idleTime > 0) {BotSpeak("Now waiting " + Math.Ceiling(idleTime*0.5).ToString() + " seconds; will continue performing stage " + routinestage.ToString() + " in routine " + currentroutine.ToString() + ".");}
                                    else {BotSpeak("Now performing stage " + routinestage.ToString() + " in routine " + currentroutine.ToString() + ".");}
                                }
                            }
                            else if (msg == "!finishroutine")
                            {
                                if (learningroutine)
                                {
                                    learningroutine = false;
                                    currentroutine = -1;
                                    routinestage = -1;
                                    BotSpeak(getSpeech(36));
                                }
                            }
                            else if (msg == "!proceed")
                            {
                                if (touring == true && touristPresent == true)
                                {
                                    tourStage++;
                                    goToTour();
                                }
                            }
                            else if (msg == "!hearstory")
                            {
                                BotSpeak("I'm listenin'!");
                                askForStory(e.OwnerID);
                            }
                            else if (msg == "!tellstory")
                            {
                                if (l.stories.Count > 0)
                                {
                                    if (currentstory == null)
                                    {
                                        int storygrab = rnd.Next(l.stories.Count);
                                        currentstory = l.stories[storygrab];
                                    }
                                    storytelling = true;
                                    listener = e.SourceID;
                                }
                                else
                                {
                                    BotSpeak(getSpeech(15));
                                }
                            }
                            else if (msg == "!weather")
                            {
                                List<string> lala = new List<string>();
                                getWeather(GetWoeid("Hamilton, CA"), out lala);
                                BotSpeak(String.Format(getSpeech(77),lala[0],lala[1],lala[2],lala[3])); //BotSpeak("The date is " + lala[0] + ". Conditions are " + lala[1] + ", with a low of " + lala[2] + " and a high of " + lala[3]);
                            }
                            else if (msg == "!wherecanada")
                            {
                                float myx, myy;
                                myx = (float)81.4217 - (Client.Self.SimPosition.X / (float)76.5098);
                                myy = (float)44.0436 + (Client.Self.SimPosition.Y / (float)76.5098);

                                try
                                {
                                    string woo = GetWhere(myy.ToString(), "-" + myx.ToString());
                                    BotSpeak("I am in " + woo);
                                }
                                catch
                                {
                                    BotSpeak("Invalid coordinates (" + myy.ToString() + ", -" + myx.ToString() + " )");
                                }
                            }
                            else { BotSpeak(getSpeech(80)); }
                        }
                        else
                        {
                            if (learningtour)
                            {
                                currenttour.tourInfo[tourStage - 1].Add(e.Message);
                                BotSpeak(getSpeech(33));
                            }
                            else if (learningroutine)
                            {
                                l.routines[currentroutine].routineSpeech[routinestage] = e.Message;
                                BotSpeak(getSpeech(33));
                            }
                            else if (listening)
                            {
                                if (msg.Contains("the end"))
                                {
                                    BotSpeak(getSpeech(11));
                                    if (waitForAnswer(true) == true)
                                    {
                                        listening = false;
                                        BotSpeak(getSpeech(12), false);
                                        DoAnimation(Animations.CLAP);
                                        System.Threading.Thread.Sleep(2000);
                                        BotSpeak(getSpeech(78), true);
                                        if (waitForAnswer(false) == true)
                                        {
                                            BotSpeak(getSpeech(46), false);
                                        }
                                        else
                                        {
                                            BotSpeak(getSpeech(44), false);
                                            currentstory.paragraphs.Clear();
                                            currentstory._id = 0;
                                            currentstory._title = null;
                                            l.stories.Remove(currentstory);
                                        }
                                        currentstory = null;
                                    }
                                    else
                                    {
                                        BotSpeak(getSpeech(13), false);
                                        DoAnimation(Animations.EMBARRASSED);
                                    }
                                }
                                else
                                {
                                    currentstory.paragraphs.Add(e.Message);
                                    BotSpeak(getSpeech(8), false);
                                    DoAnimation(Animations.YES);
                                }
                            }
                            else if (isQuestion(msg)) // If it's a question...
                            {
                                // Default response

                                say = getSpeech(39);
                                List<string> saythese = new List<string>();

                                int usedchars = 0;
                                textNode ournode = SearchNode(s, e.OwnerID, getKeywords(Simplify(msg)));
                                if (ournode != null)
                                {
                                    say = ournode._data; //ReadNode(ournode._data);
                                    saythese.Add(say);

                                    rememberTaught(e.OwnerID, ournode._id - 1);
                                    usedchars += ournode._data.Length;

                                    used.Clear();
                                    textNode next = null;
                                    bool finished = false;
                                    used.Add(ournode);

                                    while (finished == false)
                                    {
                                        finished = true;
                                        int bluh = rnd.Next(2);
                                        List<textNode> q = new List<textNode>();

                                        if (ournode.dependencies.Count > 0) // If dependent on other pieces of information...
                                        {
                                            q = GetDependentNode(e.OwnerID, ournode);
                                        }
                                        if (q.Count <= 0) { q = GetLinkedNode(ournode); }
                                        if (q.Count <= 0) { q = GetOptionNode(ournode); }

                                        if (q.Count > 0)
                                        {
                                            //int sel = rnd.Next(q.Count);
                                            foreach (textNode p in q)
                                            {
                                                if (p._data.Length < (limit - usedchars) && !used.Contains(p)) // To prevent hitting the cap.
                                                {
                                                    //say += " " + ReadNode(p._data); // I guess this is where we'll check for methods...
                                                    saythese.Add(ReadNode(p._data));
                                                    rememberTaught(e.OwnerID, p._id - 1);
                                                    usedchars += p._data.Length;
                                                    used.Add(p);
                                                    next = p;
                                                    finished = false;
                                                }
                                                ournode = next;
                                                //break;
                                            }
                                        }
                                    }
                                }
                                else { saythese.Add(getSpeech(39)); }
                                //if (say.Length > limit) { say.Remove(limit); }

                                foreach (string aaaa in saythese)
                                {
                                    BotSpeak(aaaa);
                                }
                            }
                            else if (msg.Contains("yes") || msg.Contains("yeah") || msg.Contains("yep") || msg.Contains("yup") || msg.Contains("mhm") ||
                                msg.Contains("affirmative") || msg.Contains("uh huh") || msg.Contains("yis") || msg.Contains("kay") || msg.Contains("ok") || msg.Contains("all right") ||
                                msg.Contains("sure") || msg.Contains("mmk") || msg.Contains("aye") || msg.Contains("aii"))
                            { yesno = 1; }

                            else if (msg.Contains("no") || msg.Contains("nah") || msg.Contains("naw") || msg.Contains("not") || msg.Contains("nay") ||
                                msg.Contains("don't") || msg.Contains("negative") || msg.Contains("negatory"))
                            { yesno = 0; }

                            else { handleDialogue(e.OwnerID, Simplify(msg)); } // This should happen if the bot doesn't have anything to say yet.
                        }

                    }
                }
            }
        }
        static void Self_IM(object sender, InstantMessageEventArgs e)
        {
            switch (e.IM.Dialog)
            {
                case InstantMessageDialog.MessageFromAgent:
                    // This will respond to any instant message recieved automatically.
                    //OpenMetaverse.InternalDictionary<UUID,Vector3> positions = Simulator.AvatarPositions {};
                    //Avatar av;
                    //av = Client.Network.CurrentSim.ObjectsAvatars.TryGetValue(e.IM.FromAgentID, out av);
                    lastIM = e.IM.Message;
                    lastIMer = e.IM.FromAgentID;

                    OpenMetaverse.Vector3 pos = Client.Self.SimPosition;
                    OpenMetaverse.Vector3 pos2 = e.IM.Position;
                    Avatar A = Client.Network.CurrentSim.ObjectsAvatars.Find(delegate(Avatar Av) { return Av.ID == e.IM.FromAgentID; });
                    pos2 = A.Position;
                    /*
                    Client.Network.CurrentSim.AvatarPositions.ForEach(delegate(KeyValuePair<UUID, Vector3> whoop)
                    {
                        
                        if (whoop.Key == e.IM.FromAgentID)
                        {
                            Client.Network.CurrentSim.AvatarPositions.TryGetValue(whoop.Key, out pos2);
                        }
                    });
                    */
                    
                    string say = "";
                    List<string> res = getCompoundSentences(Simplify(e.IM.Message.ToLower()));

                    foreach (string msg in res)
                    {
                        if (msg.StartsWith("!"))
                        {
                            if (msg.Contains("!tweet"))
                            {
                                string foo;
                                foo = msg.Remove(0, 6);
                                foo = foo.Trim();
                                string tw = e.IM.FromAgentName + " says: " + foo;
                                if (tw.Length < 140)
                                {
                                    Tweet(tw);
                                    BotIM(e.IM.FromAgentID,getSpeech(75));
                                }
                                else { BotIM(e.IM.FromAgentID,String.Format(getSpeech(76), tw.Length, (tw.Length - 140))); } //BotIM(e.IM.FromAgentID,"That's too long to tweet! It was " + tw.Length + " characters - " + (tw.Length-140) + " characters too many!"); }
                            }
                            else if (msg.Contains("!say"))
                            {
                                string foo = lastIM;
                                foo = foo.Remove(0,foo.IndexOf(' '));
                                foo = foo.Trim();
                                BotSpeak(foo);
                            }
                            else if (msg == "!come")
                            {
                                float butt;
                                Client.Network.CurrentSim.TerrainHeightAtPoint((int)pos2.X, (int)pos2.Y, out butt);

                                if (pos2.Z > butt + 6 && butt != 0) { Client.Self.Movement.Fly = true; }
                                else { Client.Self.Movement.Fly = false; }
                                goTo(pos2);
                            }
                            else if (msg == "!follow")
                            {
                                if (following == false)
                                {
                                    following = true; tofollow = e.IM.FromAgentID;
                                    BotIM(e.IM.FromAgentID, getSpeech(2));
                                }
                                else
                                {
                                    BotIM(e.IM.FromAgentID, getSpeech(4));
                                }
                            }
                            else if (msg == "!stop")
                            {
                                if (following == true)
                                {
                                    BotIM(e.IM.FromAgentID, getSpeech(5));
                                    following = false;
                                }
                                else
                                {
                                    BotIM(e.IM.FromAgentID, getSpeech(6));
                                }
                            }
                            else if (msg == "!teleport")
                            {
                                Client.Self.SendTeleportLure(e.IM.FromAgentID);
                                BotIM(e.IM.FromAgentID, getSpeech(30));
                            }
                            else if (msg == "!where")
                            {
                                BotIM(e.IM.FromAgentID, "I am at " + Client.Network.CurrentSim.Name + ". My position is " + pos.X + ", " + pos.Y);
                            }
                            else if (msg == "!point")
                            {
                                Client.Self.AnimationStart(Animations.POINT_YOU, true);
                            }
                            else if (msg == "!whome")
                            {
                                BotIM(e.IM.FromAgentID, "You are " + e.IM.FromAgentName + ", at position " + e.IM.Position + " in " + e.Simulator.Name);
                            }
                            else if (msg == "!look")
                            {
                                Client.Self.Movement.Camera.LookDirection(pos2);
                            }
                            else if (msg == "!whoclosest")
                            {
                                UUID closest = FindClosestAvatar();
                                string balls = closest.ToString(); // Maturity.
                                BotIM(e.IM.FromAgentID, "That would be " + balls);
                            }
                            else if (msg == "!tour")
                            {
                                if (l.tours.Count > 0)
                                {
                                    if (currenttour == null)
                                    {
                                        int tourgrab = rnd.Next(l.tours.Count);
                                        currenttour = l.tours[tourgrab];
                                    }
                                    tourist = e.IM.FromAgentID;
                                    goToTour();
                                }
                                else
                                {
                                    BotIM(e.IM.FromAgentID, getSpeech(72));
                                }
                            }
                            else if (msg == "!setidle")
                            {
                                s.idleLoc = pos2;
                            }
                            else if (msg == "!learntour")
                            {
                                currenttour = new botTours();
                                l.tours.Add(currenttour);
                                currenttour._id = l.tours.Count;
                                currenttour.enabled = false;

                                learningtour = true;
                                BotIM(e.IM.FromAgentID, getSpeech(31));
                            }
                            else if (msg == "!tourhere")
                            {
                                if (learningtour)
                                {
                                    tourStage++;
                                    List<string> woo = new List<string>();
                                    currenttour.tourInfo.Add(woo);
                                    currenttour.tourLocs.Add(pos2);
                                    BotIM(e.IM.FromAgentID, String.Format(getSpeech(32), pos2.X + ", " + pos2.Y + ", " + pos2.Z));
                                }
                            }
                            else if (msg == "!finishtour")
                            {
                                if (learningtour == true)
                                {
                                    learningtour = false;
                                    currenttour = null;
                                    BotIM(e.IM.FromAgentID, getSpeech(36));
                                }
                                else
                                {
                                    BotIM(e.IM.FromAgentID, getSpeech(37));
                                }
                            }
                            else if (msg == "!stoptour")
                            {
                                if (touring == true)
                                {
                                    touring = false;
                                    currenttour = null;
                                    BotIM(e.IM.FromAgentID, getSpeech(27));
                                }
                                else
                                {
                                    BotIM(e.IM.FromAgentID, getSpeech(28));
                                }
                            }
                            else if (msg == "!learnroutine")
                            {
                                botRoutines foo = new botRoutines();
                                l.routines.Add(foo);
                                foo._id = l.routines.Count;
                                currentroutine = foo._id - 1;

                                l.routines[currentroutine].routineLocs.Add(Vector3.Zero);
                                l.routines[currentroutine].routinePauses.Add(0);
                                l.routines[currentroutine].routineSpeech.Add("");
                                l.routines[currentroutine].routineAnimations.Add(UUID.Zero);
                                routinestage = 0;

                                learningroutine = true;
                                BotIM(e.IM.FromAgentID, getSpeech(31));
                            }
                            else if (msg == "!routehere")
                            {
                                if (learningroutine)
                                {
                                    l.routines[currentroutine].routineLocs[routinestage] = pos2;
                                    BotIM(e.IM.FromAgentID, String.Format(getSpeech(32), pos2.X + ", " + pos2.Y + ", " + pos2.Z));
                                }
                            }
                            else if (msg == "!routenextstage")
                            {
                                if (learningroutine)
                                {
                                    l.routines[currentroutine].routineLocs.Add(pos2);
                                    l.routines[currentroutine].routinePauses.Add(0);
                                    l.routines[currentroutine].routineSpeech.Add("");
                                    l.routines[currentroutine].routineAnimations.Add(UUID.Zero);
                                    routinestage++;
                                    BotIM(e.IM.FromAgentID, "Now taking notes for stage " + routinestage.ToString());
                                }
                            }
                            else if (msg == "!routinecheck")
                            {
                                if (learningroutine)
                                {
                                    BotIM(e.IM.FromAgentID, "Now taking notes for stage " + routinestage.ToString() + " in routine " + currentroutine.ToString() + ".");
                                }
                                else
                                {
                                    if (idleTime > 0) { BotIM(e.IM.FromAgentID, "Now waiting " + Math.Ceiling(idleTime * 0.5).ToString() + " seconds; will continue performing stage " + routinestage.ToString() + " in routine " + currentroutine.ToString() + "."); }
                                    else { BotIM(e.IM.FromAgentID, "Now performing stage " + routinestage.ToString() + " in routine " + currentroutine.ToString() + "."); }
                                }
                            }
                            else if (msg == "!finishroutine")
                            {
                                if (learningroutine)
                                {
                                    learningroutine = false;
                                    currentroutine = -1;
                                    routinestage = -1;
                                    BotIM(e.IM.FromAgentID, getSpeech(36));
                                }
                            }
                            else if (msg == "!proceed")
                            {
                                if (touring == true && touristPresent == true)
                                {
                                    tourStage++;
                                    goToTour();
                                }
                            }
                            else if (msg == "!hearstory")
                            {
                                BotIM(e.IM.FromAgentID, "I'm listenin'!");
                                askForStory(e.IM.FromAgentID);
                            }
                            else if (msg == "!tellstory")
                            {
                                if (l.stories.Count > 0)
                                {
                                    if (currentstory == null)
                                    {
                                        int storygrab = rnd.Next(l.stories.Count);
                                        currentstory = l.stories[storygrab];
                                    }
                                    storytelling = true;
                                    listener = e.IM.FromAgentID;
                                }
                                else
                                {
                                    BotIM(e.IM.FromAgentID, getSpeech(15));
                                }
                            }
                            else if (msg == "!weather")
                            {
                                List<string> lala = new List<string>();
                                getWeather(GetWoeid("Hamilton, CA"), out lala);
                                BotIM(e.IM.FromAgentID, String.Format(getSpeech(77), lala[0], lala[1], lala[2], lala[3])); //BotIM(e.IM.FromAgentID,"The date is " + lala[0] + ". Conditions are " + lala[1] + ", with a low of " + lala[2] + " and a high of " + lala[3]);
                            }
                            else if (msg == "!wherecanada")
                            {
                                float myx, myy;
                                myx = (float)81.4217 - (Client.Self.SimPosition.X / (float)76.5098);
                                myy = (float)44.0436 + (Client.Self.SimPosition.Y / (float)76.5098);

                                try
                                {
                                    string woo = GetWhere(myy.ToString(), "-" + myx.ToString());
                                    BotIM(e.IM.FromAgentID, "I am in " + woo);
                                }
                                catch
                                {
                                    BotIM(e.IM.FromAgentID, "Invalid coordinates (" + myy.ToString() + ", -" + myx.ToString() + " )");
                                }
                            }
                            else { BotIM(e.IM.FromAgentID, getSpeech(80)); }
                        }
                        else
                        {
                            if (learningtour)
                            {
                                currenttour.tourInfo[tourStage - 1].Add(e.IM.Message);
                                BotIM(e.IM.FromAgentID,getSpeech(33));
                            }
                            else if (learningroutine)
                            {
                                l.routines[currentroutine].routineSpeech[routinestage] = e.IM.Message;
                                BotIM(e.IM.FromAgentID,getSpeech(33));
                            }
                            else if (listening)
                            {
                                if (msg.Contains("the end"))
                                {
                                    BotIM(e.IM.FromAgentID,getSpeech(11));
                                    if (waitForAnswer(true) == true)
                                    {
                                        listening = false;
                                        BotIM(e.IM.FromAgentID,getSpeech(12), false);
                                        DoAnimation(Animations.CLAP);
                                        System.Threading.Thread.Sleep(2000);
                                        BotIM(e.IM.FromAgentID,getSpeech(78), true);
                                        if (waitForAnswer(false) == true)
                                        {
                                            BotIM(e.IM.FromAgentID,getSpeech(46), false);
                                        }
                                        else
                                        {
                                            BotIM(e.IM.FromAgentID,getSpeech(44), false);
                                            currentstory.paragraphs.Clear();
                                            currentstory._id = 0;
                                            currentstory._title = null;
                                            l.stories.Remove(currentstory);
                                        }
                                        currentstory = null;
                                    }
                                    else
                                    {
                                        BotIM(e.IM.FromAgentID,getSpeech(13), false);
                                        DoAnimation(Animations.EMBARRASSED);
                                    }
                                }
                                else if (msg.Contains("the end"))
                                {

                                }
                                else
                                {
                                    currentstory.paragraphs.Add(e.IM.Message);
                                    BotIM(e.IM.FromAgentID,getSpeech(8), false);
                                    DoAnimation(Animations.YES);
                                }
                            }
                            else if (isQuestion(msg)) // If it's a question...
                            {
                                // Default response

                                say = getSpeech(39);
                                List<string> saythese = new List<string>();

                                int usedchars = 0;
                                textNode ournode = SearchNode(e.IM.FromAgentID, getKeywords(Simplify(msg)));
                                if (ournode != null)
                                {
                                    say = ReadNode(ournode._data);
                                    saythese.Add(say);

                                    rememberTaught(e.IM.FromAgentID, ournode._id - 1);
                                    usedchars += ournode._data.Length;

                                    used.Clear();
                                    textNode next = null;
                                    bool finished = false;
                                    used.Add(ournode);

                                    while (finished == false)
                                    {
                                        finished = true;
                                        int bluh = rnd.Next(2);
                                        List<textNode> q = new List<textNode>();

                                        if (ournode.dependencies.Count > 0) // If dependent on other pieces of information...
                                        {
                                            q = GetDependentNode(e.IM.FromAgentID, ournode);
                                        }
                                        if (q.Count <= 0) { q = GetLinkedNode(ournode); }
                                        if (q.Count <= 0) { q = GetOptionNode(ournode); }

                                        if (q.Count > 0)
                                        {
                                            //int sel = rnd.Next(q.Count);
                                            foreach (textNode p in q)
                                            {
                                                if (p._data.Length < (limit - usedchars)) // To prevent hitting the cap.
                                                {
                                                    //say += " " + ReadNode(p._data); // I guess this is where we'll check for methods...
                                                    saythese.Add(ReadNode(p._data));
                                                    rememberTaught(e.IM.FromAgentID, p._id - 1);
                                                    usedchars += p._data.Length;
                                                    used.Add(p);
                                                    next = p;
                                                    finished = false;
                                                }
                                                ournode = next;
                                                //break;
                                            }
                                        }
                                    }
                                }
                                else { saythese.Add(say); }
                                //if (say.Length > limit) { say.Remove(limit); }

                                foreach (string aaaa in saythese)
                                {
                                    BotIM(e.IM.FromAgentID,aaaa);
                                }
                            }
                            else if (msg.Contains("yes") || msg.Contains("yeah") || msg.Contains("yep") || msg.Contains("yup") || msg.Contains("mhm") ||
                                msg.Contains("affirmative") || msg.Contains("uh huh") || msg.Contains("yis") || msg.Contains("kay") || msg.Contains("ok") || msg.Contains("all right") ||
                                msg.Contains("sure") || msg.Contains("mmk") || msg.Contains("aye") || msg.Contains("aii"))
                            { yesno = 1; }

                            else if (msg.Contains("no") || msg.Contains("nah") || msg.Contains("naw") || msg.Contains("not") || msg.Contains("nay") ||
                                msg.Contains("don't") || msg.Contains("negative") || msg.Contains("negatory"))
                            { yesno = 0; }

                            else { handleDialogue(e.IM.FromAgentID, Simplify(msg)); } // This should happen if the bot doesn't have anything to say yet.
                        }
                    }

                    break;

                case InstantMessageDialog.RequestTeleport:
                    if (hasAccess(e.IM.FromAgentID))
                    { Client.Self.TeleportLureRespond(e.IM.FromAgentID, e.IM.IMSessionID, true); }
                    else
                    { BotIM(e.IM.FromAgentID, "Sorry " + e.IM.FromAgentName + ", you're not authorized to do that.", e.IM.IMSessionID); }
                    break;

                case InstantMessageDialog.FriendshipOffered:
                    if (hasAccess(e.IM.FromAgentID))
                    { Client.Friends.AcceptFriendship(e.IM.FromAgentID, e.IM.IMSessionID); }
                    else
                    { BotIM(e.IM.FromAgentID, "Sorry " + e.IM.FromAgentName + ", you're not authorized to do that.", e.IM.IMSessionID); }
                    break;

                case InstantMessageDialog.AcceptTeleport:
                    //teleoffer = 1;
                    BotIM(e.IM.FromAgentID, "Woo.");
                    break;

                case InstantMessageDialog.DenyTeleport:
                    //teleoffer = 0;
                    BotIM(e.IM.FromAgentID, "Aw.");
                    break;
            }
        }

        static List<textNode> GetLinkedNode(textNode t)
        {
            List<textNode> woo = new List<textNode>();
            if (t.connections.Count > 0)
            {
                foreach (int con in t.connections)
                {
                    foreach (textNode q in l.nodes)
                    {
                        if (q._state != state && q._state != -1) { continue; } // Not sure if good.

                        if (q._id == con && !used.Contains(q))
                        {
                            woo.Add(q);
                        }
                    }
                }
            }
            return woo;
        }
        static List<textNode> GetDependentNode(textNode t)
        {
            List<textNode> woo = new List<textNode>();
            if (t.dependencies.Count > 0)
            {
                foreach (int con in t.dependencies)
                {
                    foreach (textNode q in l.nodes)
                    {
                        if (q._state != state && q._state != -1) { continue; } // Not sure if good.

                        if (q._id == con && !used.Contains(q))
                        {
                            woo.Add(q);
                        }
                    }
                }
            }
            return woo;
        }
        static List<textNode> GetOptionNode(textNode t)
        {
            List<textNode> woo = new List<textNode>();
            if (t.options.Count > 0)
            {
                int con = t.options[rnd.Next(t.options.Count)];
                foreach (textNode q in l.nodes)
                {
                    if (q._state != state && q._state != -1) { continue; } // Not sure if good.

                    if (q._id == con && !used.Contains(q))
                    {
                        woo.Add(q);
                    }
                }

            }
            return woo;
        }
        // For checking for nodes that are known by an avatar.
        static List<textNode> GetDependentNode(UUID a, textNode t)
        {
            int i = 0;
            List<textNode> woo = new List<textNode>();
            if (t.dependencies.Count > 0)
            {
                foreach (int con in t.dependencies)
                {
                    i = 0;
                    foreach (textNode q in l.nodes)
                    {
                        bool no = false;
                        if (q._state != state && q._state != -1) { continue; } // Not sure if good.
                        /*
                        if (s.mPeople.IndexOf(a) != -1)
                        {
                            int foo = s.mLearning[s.mPeople.IndexOf(a)][i];
                            if (foo > 0) { no = true; } // They know it; don't bother.
                        }
                        */
                        if (no == true) { }
                        else if (q._id == con && !used.Contains(q))
                        {
                            woo.Add(q);
                        }

                        i++;
                    }
                }
            }
            return woo;
        }

        private void MockingBOT_MouseClick(object sender, MouseEventArgs e)
        {
            
        }

        private void MockingBOT_KeyDown(object sender, KeyEventArgs e)
        {
        }
        private void MockingBOT_KeyPress(object sender, KeyPressEventArgs e)
        {
        }

        private void drawSet1_Click(object sender, EventArgs e)
        {
            drawType = 0;
        }
        private void drawSet2_Click(object sender, EventArgs e)
        {
            drawType = 1;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            drawType = 2;
        }
        private void drawSet4_Click(object sender, EventArgs e)
        {
            drawType = 5;
        }

        private void trashButton_Click(object sender, EventArgs e)
        {
            if (nodesel != -1)
            {
                DialogResult foo = MessageBox.Show("Are you sure you want to delete this node?", "MockingBOT", MessageBoxButtons.YesNo);
                if (foo == DialogResult.Yes)
                {
                    textNode victim = null;
                    foreach (textNode t in l.nodes)
                    {
                        if (t._id == nodesel)
                        {
                            victim = t;
                        }
                    }
                    l.nodes.Remove(victim);
                    // DO SOMETHING HERE
                    foreach (botSettings b in l.bots)
                    {
                        for (int i = 0; i < b.mLearning.Count; i++)
                        {
                            if (b.mLearning.Count <= 0) { break; }
                            if (b.mLearning[i].Count >= nodesel) { b.mLearning[i][nodesel - 1] = 0; }
                        }
                    }

                    nodesel = -1;
                    for (int i = 0; i < botsListNodes.Items.Count; i++) { botsListNodes.SetSelected(i, false); }

                    Refresh();
                }
            }
        }

        public static string Wrap(string text, int maxLength, Graphics g, Font f)
        {
            string nt = text;
            // Return empty list of strings if the text was empty
            if (nt.Length == 0) return "";

            if (nt.Length > 60) { nt = nt.Remove(60); nt = nt.Insert(nt.Length, "..."); }

            var words = nt.Split(' ');
            //var lines = new List<string>();
            var currentLine = "";
            StringBuilder sb = new StringBuilder();

            foreach (var currentWord in words)
            {
                int wid1 = (int)g.MeasureString(currentLine, f).Width;
                int wid2 = (int)g.MeasureString(currentLine, f).Width;

                if ((wid1 > maxLength) || (wid2 > maxLength))
                {
                    sb.AppendLine(currentLine);
                    currentLine = "";
                }

                if (currentLine.Length > 0)
                    currentLine += " " + currentWord;
                else
                    currentLine += currentWord;

            }

            if (currentLine.Length > 0)
            { sb.AppendLine(currentLine); }


            return sb.ToString();
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && nodesel != -1)
            {
                foreach (textNode t in l.nodes)
                {
                    if (t._id == nodesel)
                    {
                        t._x = e.X + grabx;
                        t._y = e.Y + graby;
                        Refresh();
                    }
                }
            }
            if (e.Button == MouseButtons.Right)
            {
                drawx += mxPrev - e.X;
                drawy += myPrev - e.Y;

                int xmin = 0; int xmax = 0; int ymin = 0; int ymax = 0;
                if (drawx < xmin) { drawx = xmin; }
                if (drawy < ymin) { drawy = ymin; }
                if (drawx > 32000) { drawx = 32000; }
                if (drawy > 32000) { drawy = 32000; }

                Refresh();
            }

            mxPrev = e.X;
            myPrev = e.Y;
        }
        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            {
                if (e.Button == MouseButtons.Left)
                {
                    bool collide = false;
                    textNode col = null;
                    foreach (textNode t in l.nodes)
                    {
                        float newx = (float)(t._x - t._width * 0.5);
                        float newy = (float)(t._y - t._height * 0.5);
                        col = t;

                        if (e.X + drawx > newx && e.Y + drawy > newy && e.X + drawx < newx + t._width && e.Y + drawy < newy + t._height)
                        {
                            collide = true;
                            if (drawType == 1 || drawType == 2 || drawType == 3 || drawType == 4 || drawType == 5 || drawType == 6)
                            {
                                if (nodeselp == -1)
                                {
                                    nodesel = t._id;
                                    nodeselp = t._id;
                                }
                                else
                                {
                                    foreach (textNode q in l.nodes)
                                    {
                                        if (q._id == nodeselp)
                                        {
                                            if (q._id == t._id) { nodeselp = -1; nodesel = -1; }
                                            else
                                            {
                                                if (drawType == 1) // Add Link
                                                {
                                                    q.connections.Add(t._id);
                                                    t.connections.Add(q._id);
                                                }
                                                else if (drawType == 2) // Add Dependency
                                                {
                                                    q.dependencies.Add(t._id);
                                                }
                                                else if (drawType == 3) // Break Link
                                                {
                                                    foreach (int con in q.connections)
                                                    {
                                                        if (con == t._id) { q.connections.Remove(con); t.connections.Remove(q._id); break; }
                                                    }
                                                }
                                                else if (drawType == 4) // Break Dependency
                                                {
                                                    foreach (int dep in q.dependencies)
                                                    {
                                                        if (dep == t._id) { q.dependencies.Remove(dep); break; }
                                                    }
                                                }
                                                else if (drawType == 5) // Add Option
                                                {
                                                    q.options.Add(t._id);
                                                }
                                                else if (drawType == 6) // Break Option
                                                {
                                                    foreach (int con in q.options)
                                                    {
                                                        if (con == t._id) { q.connections.Remove(con); t.connections.Remove(q._id); break; }
                                                    }
                                                }
                                                nodeselp = -1;
                                                nodesel = -1;
                                            }
                                        }
                                    }
                                }
                            }
                            else {
                                nodesel = t._id; textEntry1.Text = t._data; subjectLine1.Text = t._subject; nodeselp = nodesel;
                                stateBox1.Text = t._state.ToString();
                                IDBox1.Text = t._id.ToString();
                                grabx = (int)(t._x - e.X); graby = (int)(t._y - e.Y);
                            }
                        }
                    }


                    if (collide == true)
                    {
                        ActiveControl = textEntry1;
                        //textEntry1.Text = col._data;
                        Refresh();
                    }
                    else
                    {
                        if (drawType == 0)
                        {
                            // Spawn a box
                            textNode biscuit = new textNode();
                            l.nodes.Add(biscuit);

                            int newid = 0;
                            for (int i = 0; i < l.nodes.Count+1; i++)
                            {
                                bool usedid = false;
                                for (int ii = 0; ii < l.nodes.Count; ii++)
                                {
                                    if (l.nodes[ii]._id == i) { usedid = true; break; }
                                }
                                if (!usedid) { newid = i; break; }
                            }
                            biscuit._id = newid; // l.nodes.Count;
                            
                            biscuit._width = 100;
                            biscuit._height = 30;
                            biscuit._state = -1;
                            biscuit._x = e.X+drawx;
                            biscuit._y = e.Y+drawy;
                            
                            nodesel = biscuit._id;
                            nodeselp = biscuit._id;
                            ActiveControl = subjectLine1;
                            textEntry1.Text = "";
                            subjectLine1.Text = "";

                            for (int i = 0; i < l.bots.Count; i++)
                            {
                                l.bots[i].allowednodes.Add(biscuit._id);
                                for (int ii = 0; ii < l.bots[i].mPeople.Count; ii++) { l.bots[i].mLearning[ii].Add(0); }
                            }
                            for (int i = 0; i < botsListNodes.Items.Count; i++) { botsListNodes.SetSelected(i, true);}

                            this.Refresh();
                        }
                    }
                }
                else
                {
                    ActiveControl = null;
                    nodesel = -1;
                    drawType = -1;
                    this.Refresh();
                }

                if (nodesel != -1)
                {
                    for (int i = 0; i < l.bots.Count; i++)
                    {
                        if (l.bots[i].allowednodes.Contains(nodesel))
                        {
                            botsListNodes.SetSelected(i, true);
                        }
                        else { botsListNodes.SetSelected(i, false); }
                    }
                }
                else { for (int i = 0; i < botsListNodes.Items.Count; i++) { botsListNodes.SetSelected(i, false); } }

            }
        }
        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            Color outline = new Color();
            Color fill = new Color();

            Brush text = new SolidBrush(Color.Black);

            Pen link = new Pen(Color.Blue, 5);
            Pen dep = new Pen(Color.Red, 3);
            Pen opt = new Pen(Color.Gold, 5);
            dep.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
            link.DashCap = System.Drawing.Drawing2D.DashCap.Round;
            link.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            int i = 0; int ii = 0;
            foreach (textNode t in l.nodes)
            {
                foreach (int con in t.connections)
                {
                    ii = 0;
                    foreach (textNode q in l.nodes)
                    {
                        if (q._id == con && ii < i)
                        {
                            e.Graphics.DrawLine(link, t._x - drawx, t._y + 3 - drawy, q._x - drawx, q._y + 3 - drawy);
                        }
                        ii++;
                    }
                }
                foreach (int d in t.dependencies)
                {
                    foreach (textNode q in l.nodes)
                    {
                        if (q._id == d)
                        {
                            e.Graphics.DrawLine(dep, t._x - drawx, t._y - 3 - drawy, q._x - drawx, q._y - 6 - drawy);
                            e.Graphics.DrawLine(dep, t._x - drawx, t._y - 3 - drawy, q._x - drawx, q._y - drawy);
                        }
                    }
                }
                foreach (int o in t.options)
                {
                    foreach (textNode q in l.nodes)
                    {
                        if (q._id == o)
                        {
                            e.Graphics.DrawLine(opt, t._x - drawx, t._y - drawy, q._x - drawx, q._y - drawy);
                        }
                    }
                }

                i++;
            }

            link.Dispose();
            dep.Dispose();

            foreach (textNode t in l.nodes)
            {
                Color col = new Color();
                Color dcol = new Color();
                col = Color.Gold;
                dcol = Color.Goldenrod;
                if (Color.FromArgb(t._color) != Color.Empty && t._color != 0)
                {
                    col = Color.FromArgb(t._color);
                    dcol = ControlPaint.Dark(Color.FromArgb(t._color));
                }
                if (nodesel == t._id) { outline = Color.White; fill = col; } else { outline = col; fill = dcol; }

                string wrapped = null;

                if (!String.IsNullOrEmpty(t._data))
                {
                    wrapped = Wrap(t._data, 150, e.Graphics, f);
                }
                else { wrapped = ""; }

                int swidth = (int)e.Graphics.MeasureString(wrapped, f).Width;
                if (t._width < swidth + 12) { t._width = swidth + 12; }
                if (t._width > swidth + 12) { t._width = swidth + 12; }
                if (t._width < 100) { t._width = 100; }

                int sheight = (int)e.Graphics.MeasureString(wrapped, f).Height;
                if (t._height < sheight + 12) { t._height = sheight + 12; }
                if (t._height > sheight + 12) { t._height = sheight + 12; }
                if (t._height < 30) { t._height = 30; }


                Pen pen2 = new Pen(outline, 2);
                Brush brush = new SolidBrush(fill);

                float newx = (float)(t._x - drawx - t._width * 0.5);
                float newy = (float)(t._y - drawy - t._height * 0.5);

                e.Graphics.FillRectangle(brush, newx, newy, t._width, t._height);
                e.Graphics.DrawRectangle(pen2, newx, newy, t._width, t._height);

                e.Graphics.DrawString(wrapped, f, text, newx + 6, newy + 6);
            }

            e.Graphics.DrawImage(g, 0, 0);
        }

        // Old, unused drawing events (from when I wasn't sure where I wanted painting to happen...)
        private void MockingBOT_MouseMove(object sender, MouseEventArgs e)
        {
        }
        private void MockingBOT_MouseDown(object sender, MouseEventArgs e)
        {
            /*
            if (e.Button == MouseButtons.Left)
            {
                bool collide = false;
                textNode col = null;
                foreach (textNode t in l.nodes)
                {
                    float newx = (float)(t._x - t._width * 0.5);
                    float newy = (float)(t._y - t._height * 0.5);
                    col = t;

                    if (e.X > newx && e.Y > newy && e.X < newx + t._width && e.Y < newy + t._height)
                    {
                        collide = true;
                        if (drawType == 1 || drawType == 2 || drawType == 3 || drawType == 4)
                        {
                            if (nodeselp == -1)
                            {
                                nodesel = t._id;
                                nodeselp = t._id;
                            }
                            else
                            {
                                foreach (textNode q in l.nodes)
                                {
                                    if (q._id == nodeselp)
                                    {
                                        if (q._id == t._id) { nodeselp = -1; nodesel = -1; }
                                        else
                                        {
                                            if (drawType == 1) // Add Link
                                            {
                                                q.connections.Add(t._id);
                                                t.connections.Add(q._id);
                                            }
                                            else if (drawType == 2) // Add Dependency
                                            {
                                                q.dependencies.Add(t._id);
                                            }
                                            else if (drawType == 3) // Break Link
                                            {
                                                foreach (int con in q.connections)
                                                {
                                                    if (con == t._id) { q.connections.Remove(con); t.connections.Remove(q._id); break; }
                                                }
                                            }
                                            else if (drawType == 4) // Break Dependency
                                            {
                                                foreach (int dep in q.dependencies)
                                                {
                                                    if (dep == t._id) { q.dependencies.Remove(dep); break; }
                                                }
                                            }
                                            nodeselp = -1;
                                            nodesel = -1;
                                        }
                                    }
                                }
                            }
                        }
                        else { nodesel = t._id; textEntry1.Text = t._data; subjectLine1.Text = t._subject; nodeselp = nodesel; }
                    }
                }

                if (collide == true)
                {
                    ActiveControl = textEntry1;
                    //textEntry1.Text = col._data;
                    Refresh();
                }
                else
                {
                    if (drawType == 0)
                    {
                        // Spawn a box
                        textNode biscuit = new textNode(e.X, e.Y);
                        nodesel = biscuit._id;
                        nodeselp = biscuit._id;
                        ActiveControl = subjectLine1;
                        textEntry1.Text = "";
                        subjectLine1.Text = "";
                        this.Refresh();
                    }
                }
            }
            else
            {
                ActiveControl = null;
                nodesel = -1;
                drawType = -1;
                this.Refresh();
            }
            */
        }
        private void MockingBOT_Paint(object sender, PaintEventArgs e)
        {
        }
        private void tabPage1_Paint(object sender, PaintEventArgs e)
        {
            /*
            //System.Drawing.Graphics g;
            //g = this.CreateGraphics();
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            Color outline = new Color();
            Color fill = new Color();

            Pen link = new Pen(Color.Blue, 5);
            Pen dep = new Pen(Color.Red, 5);
            link.DashCap = System.Drawing.Drawing2D.DashCap.Round;
            link.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            Brush text = new SolidBrush(Color.Black);

            //e.Graphics.DrawString(ActiveControl.Name, new Font("Arial", 10), text, 70, 40);

            foreach (textNode t in l.nodes)
            {
                foreach (int con in t.connections)
                {
                    foreach (textNode q in s.nodes)
                    {
                        if (q._id == con)
                        {
                            e.Graphics.DrawLine(link, t._x - t._id, t._y - t._id, q._x, q._y);
                        }
                    }
                }
                foreach (int d in t.dependencies)
                {
                    foreach (textNode q in s.nodes)
                    {
                        if (q._id == d)
                        {
                            e.Graphics.DrawLine(dep, t._x + t._id, t._y + t._id, q._x, q._y);
                        }
                    }
                }
            }

            link.Dispose();
            dep.Dispose();

            foreach (textNode t in s.nodes)
            {
                Color col = new Color();
                Color dcol = new Color();
                col = Color.Gold;
                dcol = Color.Goldenrod;
                if (Color.FromArgb(t._color) != Color.Empty && t._color != 0)
                {
                    col = Color.FromArgb(t._color);
                    dcol = ControlPaint.Dark(Color.FromArgb(t._color));
                }
                if (nodesel == t._id) { outline = Color.White; fill = col; } else { outline = col; fill = dcol; }

                string wrapped = null;

                if (!String.IsNullOrEmpty(t._data))
                {
                    wrapped = Wrap(t._data, 150, e.Graphics, f);
                }
                else { wrapped = ""; }

                int swidth = (int)e.Graphics.MeasureString(wrapped, f).Width;
                if (t._width < swidth + 12) { t._width = swidth + 12; }
                if (t._width > swidth + 12) { t._width = swidth + 12; }
                if (t._width < 100) { t._width = 100; }

                int sheight = (int)e.Graphics.MeasureString(wrapped, f).Height;
                if (t._height < sheight + 12) { t._height = sheight + 12; }
                if (t._height > sheight + 12) { t._height = sheight + 12; }
                if (t._height < 30) { t._height = 30; }


                Pen pen2 = new Pen(outline, 2);
                Brush brush = new SolidBrush(fill);

                float newx = (float)(t._x - t._width * 0.5);
                float newy = (float)(t._y - t._height * 0.5);

                e.Graphics.FillRectangle(brush, newx, newy, t._width, t._height);
                e.Graphics.DrawRectangle(pen2, newx, newy, t._width, t._height);

                e.Graphics.DrawString(wrapped, f, text, newx + 6, newy + 6);
            }

            e.Graphics.DrawImage(g, 0, 0);
            */
        }
        private void tabPage1_MouseDown(object sender, MouseEventArgs e)
        {
            /*
            if (e.Button == MouseButtons.Left)
            {
                bool collide = false;
                textNode col = null;
                foreach (textNode t in s.nodes)
                {
                    float newx = (float)(t._x - t._width * 0.5);
                    float newy = (float)(t._y - t._height * 0.5);
                    col = t;

                    if (e.X > newx && e.Y > newy && e.X < newx + t._width && e.Y < newy + t._height)
                    {
                        collide = true;
                        if (drawType == 1 || drawType == 2 || drawType == 3 || drawType == 4)
                        {
                            if (nodeselp == -1)
                            {
                                nodesel = t._id;
                                nodeselp = t._id;
                            }
                            else
                            {
                                foreach (textNode q in s.nodes)
                                {
                                    if (q._id == nodeselp)
                                    {
                                        if (q._id == t._id) { nodeselp = -1; nodesel = -1; }
                                        else
                                        {
                                            if (drawType == 1) // Add Link
                                            {
                                                q.connections.Add(t._id);
                                                t.connections.Add(q._id);
                                            }
                                            else if (drawType == 2) // Add Dependency
                                            {
                                                q.dependencies.Add(t._id);
                                            }
                                            else if (drawType == 3) // Break Link
                                            {
                                                foreach (int con in q.connections)
                                                {
                                                    if (con == t._id) { q.connections.Remove(con); t.connections.Remove(q._id); break; }
                                                }
                                            }
                                            else if (drawType == 4) // Break Dependency
                                            {
                                                foreach (int dep in q.dependencies)
                                                {
                                                    if (dep == t._id) { q.dependencies.Remove(dep); break; }
                                                }
                                            }
                                            nodeselp = -1;
                                            nodesel = -1;
                                        }
                                    }
                                }
                            }
                        }
                        else { 
                            nodesel = t._id;
                            nodeselp = nodesel; 
                            textEntry1.Text = t._data; 
                            subjectLine1.Text = t._subject;
                            stateBox1.Text = t._state.ToString();
                            IDBox1.Text = t._id.ToString();
                        }
                    }
                }

                if (collide == true)
                {
                    ActiveControl = textEntry1;
                    //textEntry1.Text = col._data;
                    Refresh();
                }
                else
                {
                    if (drawType == 0)
                    {
                        // Spawn a box
                        textNode biscuit = new textNode(e.X, e.Y);
                        nodesel = biscuit._id;
                        nodeselp = biscuit._id;
                        ActiveControl = subjectLine1;
                        textEntry1.Text = "";
                        subjectLine1.Text = "";
                        stateBox1.Text = biscuit._state.ToString();
                        IDBox1.Text = biscuit._id.ToString();
                        this.Refresh();
                    }
                }
            }
            else
            {
                ActiveControl = null;
                nodesel = -1;
                drawType = -1;
                this.Refresh();
            }
            */
        }
        private void tabPage1_MouseMove(object sender, MouseEventArgs e)
        {
            /*
            if (e.Button == MouseButtons.Left && nodesel != -1)
            {
                foreach (textNode t in s.nodes)
                {
                    if (t._id == nodesel)
                    {
                        t._x = e.X;
                        t._y = e.Y;
                        Refresh();
                    }
                }
            }
            */
        }
        
        public void Serialize(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(l.nodes.Count);
            foreach (textNode t in l.nodes)
            {
                writer.Write(t._x);
                writer.Write(t._y);
                writer.Write(t._id);
                writer.Write(t._data);
                writer.Write(t._width);
                writer.Write(t._height);
            }
            writer.Flush();
        }

        private void importButton_Click_1(object sender, EventArgs e)
        {
            OpenFileDialog yay = new OpenFileDialog();
            yay.Title = "MockingBOT";
            yay.InitialDirectory = Environment.CurrentDirectory;
            if (!Directory.Exists(yay.InitialDirectory))
            {
                Directory.CreateDirectory(yay.InitialDirectory);
            }
            yay.Filter = "Xml Files (*.xml)|*.xml";
            if (yay.ShowDialog() == DialogResult.OK)
            {
                LoadSettings(yay.FileName);
                this.Refresh();
            }
        }
        private void exportButton_Click(object sender, EventArgs e)
        {
            SaveFileDialog yay = new SaveFileDialog();
            yay.Title = "MockingBOT";
            yay.InitialDirectory = Environment.CurrentDirectory;
            if (!Directory.Exists(yay.InitialDirectory))
            {
                Directory.CreateDirectory(yay.InitialDirectory);
            }
            yay.Filter = "Xml Files (*.xml)|*.xml";
            if (yay.ShowDialog() == DialogResult.OK)
            {
                SaveSettings(yay.FileName);
            }
        }
        public void SaveNodes(string path)
        {
            XmlSerializer sav = new XmlSerializer(typeof(List<textNode>));
            using (var stream = new FileStream(path, FileMode.Create))
            {
                sav.Serialize(stream, l.nodes);
                stream.Close();
            }
        }
        public void LoadNodes(string path)
        {
            l.nodes.Clear();
            object obj = new object();

            XmlSerializer d = new XmlSerializer(typeof(List<textNode>));
            using (var stream = new FileStream(path, FileMode.Open))
            {
                try
                {
                    obj = d.Deserialize(stream);
                }
                catch
                {
                    MessageBox.Show("Loading failed.");
                }
            }
        }

        private void importButton2_Click(object sender, EventArgs e)
        {
            OpenFileDialog yay = new OpenFileDialog();
            yay.Title = "MockingBOT";
            yay.InitialDirectory = Environment.CurrentDirectory;
            if (!Directory.Exists(yay.InitialDirectory))
            {
                Directory.CreateDirectory(yay.InitialDirectory);
            }
            yay.Filter = "Xml Files (*.xml)|*.xml";
            if (yay.ShowDialog() == DialogResult.OK)
            {
                LoadSettings(yay.FileName);
                this.Refresh();
            }
        }
        private void exportButton2_Click(object sender, EventArgs e)
        {
            SaveFileDialog yay = new SaveFileDialog();
            yay.Title = "MockingBOT";
            yay.InitialDirectory = Environment.CurrentDirectory;
            if (!Directory.Exists(yay.InitialDirectory))
            {
                Directory.CreateDirectory(yay.InitialDirectory);
            }
            yay.Filter = "Xml Files (*.xml)|*.xml";
            if (yay.ShowDialog() == DialogResult.OK)
            {
                SaveSettings(yay.FileName);
            }
        }
        private void SaveStories(string path)
        {
            XmlSerializer sav = new XmlSerializer(typeof(List<botStories>));
            using (var stream = new FileStream(path, FileMode.Create))
            {
                sav.Serialize(stream, l.stories);
                stream.Close();
            }
        }
        private void LoadStories(string path)
        {
            l.stories.Clear();
            object obj = new object();

            XmlSerializer d = new XmlSerializer(typeof(List<botStories>));
            using (var stream = new FileStream(path, FileMode.Open))
            {
                try
                {
                    obj = d.Deserialize(stream);
                }
                catch
                {
                    MessageBox.Show("Loading failed.");
                }
            }

            updateStories();
        }

        private void importButton4_Click(object sender, EventArgs e)
        {
            OpenFileDialog yay = new OpenFileDialog();
            yay.Title = "MockingBOT";
            yay.InitialDirectory = Environment.CurrentDirectory;
            if (!Directory.Exists(yay.InitialDirectory))
            {
                Directory.CreateDirectory(yay.InitialDirectory);
            }
            yay.Filter = "Xml Files (*.xml)|*.xml";
            if (yay.ShowDialog() == DialogResult.OK)
            {
                LoadSettings(yay.FileName);
                this.Refresh();
            }
        }
        private void exportButton4_Click(object sender, EventArgs e)
        {
            SaveFileDialog yay = new SaveFileDialog();
            yay.Title = "MockingBOT";
            yay.InitialDirectory = Environment.CurrentDirectory;
            if (!Directory.Exists(yay.InitialDirectory))
                Directory.CreateDirectory(yay.InitialDirectory);
            yay.Filter = "Xml Files (*.xml)|*.xml";
            if (yay.ShowDialog() == DialogResult.OK)
                SaveSettings(yay.FileName);
        }
        public void SaveTours(string path)
        {
            XmlSerializer sav = new XmlSerializer(typeof(List<botTours>));
            using (var stream = new FileStream(path, FileMode.Create))
            {
                sav.Serialize(stream, l.tours);
                stream.Close();
            }
        }
        public void LoadTours(string path)
        {
            l.tours.Clear();
            object obj = new object();

            XmlSerializer d = new XmlSerializer(typeof(List<botTours>));
            using (var stream = new FileStream(path, FileMode.Open))
            {
                try
                {
                    obj = d.Deserialize(stream);
                }
                catch
                {
                    MessageBox.Show("Loading failed.");
                }
            }
        }

        private void importButton3_Click(object sender, EventArgs e)
        {
            OpenFileDialog yay = new OpenFileDialog();
            yay.Title = "MockingBOT";
            yay.InitialDirectory = Environment.CurrentDirectory;
            if (!Directory.Exists(yay.InitialDirectory))
            {
                Directory.CreateDirectory(yay.InitialDirectory);
            }
            yay.Filter = "Xml Files (*.xml)|*.xml";
            if (yay.ShowDialog() == DialogResult.OK)
            {
                LoadSettings(yay.FileName);
                this.Refresh();
            }
        }
        private void exportButton3_Click(object sender, EventArgs e)
        {
            SaveFileDialog yay = new SaveFileDialog();
            yay.Title = "MockingBOT";
            yay.InitialDirectory = Environment.CurrentDirectory;
            if (!Directory.Exists(yay.InitialDirectory))
            {
                Directory.CreateDirectory(yay.InitialDirectory);
            }
            yay.Filter = "Xml Files (*.xml)|*.xml";
            if (yay.ShowDialog() == DialogResult.OK)
            {
                SaveSettings(yay.FileName);
            }
        }
        public void SaveSettings(string path)
        {
            l._default = botListBox1.SelectedIndex;

            XmlSerializer sav = new XmlSerializer(typeof(BotStorage));
            using (var stream = new FileStream(path, FileMode.Create))
            {
                sav.Serialize(stream, l);
                stream.Close();
                file = path;
                string foo = file.Remove(file.LastIndexOf('\\')+1);
                Directory.SetCurrentDirectory(foo);
            }
        }
        public void LoadSettings(string path)
        {
            object obj = new object();

            XmlSerializer d = new XmlSerializer(typeof(BotStorage));
            using (var stream = new FileStream(path, FileMode.Open))
            {
                try
                {
                    l = (BotStorage)d.Deserialize(stream);

                    if (l.bots.Count > 0)
                    {
                        s = l.bots[l._default];
                        speechSettings.DataSource = s.speechnames;
                        movementType1.SelectedIndex = s.moveType;
                        botType1.SelectedIndex = s.botType;
                    }

                    file = path;
                    string foo = file.Remove(file.LastIndexOf('\\') + 1);
                    Directory.SetCurrentDirectory(foo);
                }
                catch
                {
                    MessageBox.Show("Loading failed.");
                }
            }
        }
        
        private void trashButton2_Click(object sender, EventArgs e)
        {
            drawType = 3;
        }
        private void trashButton3_Click(object sender, EventArgs e)
        {
            drawType = 4;
        }
        private void trashButton4_Click(object sender, EventArgs e)
        {
            drawType = 6;
        }

        private void stateBox1_TextChanged(object sender, EventArgs e)
        {
            if (nodesel != -1)
            {
                foreach (textNode t in l.nodes)
                {
                    if (t._id == nodesel)
                    {
                        int floop = new int();
                        int.TryParse(stateBox1.Text, out floop);
                        t._state = floop;
                        //Refresh();
                    }
                }
            }
        }

        // Colour Node
        private void button1_Click_1(object sender, EventArgs e)
        {
            ColorDialog col = new ColorDialog();
            col.ShowDialog();
            if (nodesel != -1)
            {
                foreach (textNode t in l.nodes)
                {
                    if (t._id == nodesel)
                    {
                        t._color = col.Color.ToArgb();
                        Refresh();
                    }
                }
            }
        }

        private void textEntry1_TextChanged(object sender, EventArgs e)
        {
            if (nodesel != -1)
            {
                foreach (textNode t in l.nodes)
                {
                    if (t._id == nodesel)
                    {
                        t._data = textEntry1.Text;
                        pictureBox1.Refresh(); 
                    }
                }
            }
        }
        private void subjectLine1_TextChanged(object sender, EventArgs e)
        {
            if (nodesel != -1)
            {
                foreach (textNode t in l.nodes)
                {
                    if (t._id == nodesel)
                    {
                        t._subject = subjectLine1.Text;
                        Refresh();
                    }
                }
            }
        }

        private void gridButton2_CheckedChanged(object sender, EventArgs e)
        {
            Client.Settings.LOGIN_SERVER = "http://199.241.161.254:9000/";
        }
        private void gridButton1_CheckedChanged(object sender, EventArgs e)
        {
            Client.Settings.LOGIN_SERVER = "https://login.agni.lindenlab.com/cgi-bin/login.cgi";
        }
        
        public void ComboBoxPopulate(ComboBox ctrl, List<string> list)
        {
            if (ctrl.InvokeRequired)
            {
                ctrl.BeginInvoke(new ComboBoxPopulateCallback(ComboBoxPopulate), ctrl, list); // Invokes Control with a delegate that points to SetText as well. So it runs twice, basically.
            }
            else
            {
                ctrl.DataSource = list;
            }
        }
        delegate void ComboBoxPopulateCallback(ComboBox ctrl, List<String> list);

        private void addSpeechSetting_Click(object sender, EventArgs e)
        {
            string woo = "";
            if (speechSettings.Text != String.Empty) { woo = speechSettings.Text; }
            else { woo = "Default"; }
            s.speechnames.Add(woo);
            defaultSpeechAdd();
        }
        private void speechList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (speechList.SelectedIndex > -1) { speechChangeBox.Text = s.speech[s.currentspeech][speechList.SelectedIndex]; }
        }
        private void updateSpeech_Click(object sender, EventArgs e)
        {
            s.speech[s.currentspeech][speechList.SelectedIndex] = speechChangeBox.Text;
        }
        private void speechSettings_SelectedIndexChanged(object sender, EventArgs e)
        {
            s.currentspeech = speechSettings.SelectedIndex;
            if (s.speech[s.currentspeech].Count > speechList.SelectedIndex && speechList.SelectedIndex != -1) { speechChangeBox.Text = s.speech[s.currentspeech][speechList.SelectedIndex]; }
        }

        private void updateStories()
        {
            int ind = storyList1.SelectedIndex;
            storyList1.Items.Clear();
            foreach (botStories story in l.stories) { storyList1.Items.Add(story._title); }
            storyList1.SelectedIndex = ind;
            if (storyList1.SelectedIndex != -1)
            {
                botStories story = l.stories[storyList1.SelectedIndex];
                fullStoryText.Text = "";
                foreach (string para in story.paragraphs) { fullStoryText.Text += " " + para + Environment.NewLine; }
            }
        }
        private void storyList1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int la = storyList1.SelectedIndex; 
            
            if (la != -1)
            {
                botStories story = l.stories[la];
                //storyStageList1.Items.Clear();
                fullStoryText.Text = "";
                foreach (string para in story.paragraphs) { fullStoryText.Text += " " + para + Environment.NewLine; }
                
                int sel = l.stories[la]._id;
                for (int i = 0; i < l.bots.Count; i++)
                {
                    if (l.bots[i].allowedtours.Contains(sel))
                    {
                        botsListStories.SetSelected(i, true);
                    }
                    else { botsListStories.SetSelected(i, false); }
                }
            }
            else { for (int i = 0; i < botsListStories.Items.Count; i++) { botsListStories.SetSelected(i, false); } }
        }
        private void storyStageList1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //botStories story = botStoriel.stories[storyList1.SelectedIndex];
            //storyStageText.Text = story.paragraphs[storyStageList1.SelectedIndex];
        }
        private void button1_Click_2(object sender, EventArgs e)
        {
            if (storyList1.SelectedIndex != -1)
            {
                botStories story = l.stories[storyList1.SelectedIndex];
                //story.paragraphs[storyStageList1.SelectedIndex] = storyStageText.Text;
                string[] foo = fullStoryText.Text.Split(new string[] { Environment.NewLine }, System.StringSplitOptions.RemoveEmptyEntries);
                story.paragraphs.Clear();
                for (int i = 0; i < foo.Length; i++) { story.paragraphs.Add(foo[i].Trim()); }

                updateStories();
            }
        }
        private void addStoryButton_Click(object sender, EventArgs e)
        {
            botStories foo = new botStories();
            l.stories.Add(foo);
            foo._id = l.stories.Count;
            foo._title = "Default";

            updateStories();
            storyList1.SelectedIndex = l.stories.Count - 1;
        }
        private void storyList1_DropDown(object sender, EventArgs e)
        {
            updateStories();
            //MessageBox.Show(l.stories.Count.ToString());
        }

        private void accessListAdd_Click(object sender, EventArgs e)
        {
            UUID woo = UUID.Zero;
            if (UUID.TryParse(accessListBox1.Text, out woo))
            {
                if (!s.alist.Contains(woo))
                {
                    s.alist.Add(woo);
                    accessListBox1.Text = "";
                }
            }
            else
            {
                MessageBox.Show("Invalid UUID");
            }
        }
        private void accessList1_SelectedIndexChanged(object sender, EventArgs e)
        {
            accessListBox1.Text = s.alist[accessList1.SelectedIndex].ToString();
        }

        private void button1_Click_3(object sender, EventArgs e)
        {
            List<string> lala = new List<string>();
            getWeather(GetWoeid("43.256562", "-79.867733"), out lala);
            MessageBox.Show("The date is " + lala[0] + ". Conditions are " + lala[1] + ", with a low of " + lala[2] + " and a high of " + lala[3]);
        }
        private void button2_Click(object sender, EventArgs e)
        {
            MessageBox.Show(getWikipedia(debugQuestionBox.Text));
        }

        private void clearIdle_Click(object sender, EventArgs e)
        {
            s.idleLoc = Vector3.Zero;
        }

        private void movementType1_SelectedIndexChanged(object sender, EventArgs e)
        {
            s.moveType = movementType1.SelectedIndex;
        }
        private void botType1_SelectedIndexChanged(object sender, EventArgs e)
        {
            s.botType = botType1.SelectedIndex;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string woo = Simplify(debugQuestionBox.Text);
            if (isQuestion(woo)) {
                MessageBox.Show(woo);
                string lala = "";
                List<string> whee = getKeywords(woo);
                foreach (string w in whee) { lala += w + ", "; }
                MessageBox.Show(lala);
            }
            else {MessageBox.Show("Not a question.");}
        }
        private void debugGetSubject_Click(object sender, EventArgs e)
        {
            string[] foo = Simplify(debugQuestionBox.Text).Split(' ');
            string[] woo = Simplify(debugQuestionBox.Text).Split(' ');
            string bar = "";
            string keys = "";
            int wordsknown = s.wordsKnown.Count;
            foreach (string w in getKeywords(Simplify(debugQuestionBox.Text))) { keys += w + " "; }
            
            // Here's the Noun Verb Noun Pronoun[...] thing.
            for (int i = 0; i < foo.Length; i++) {
                string w = foo[i];
                string whee = new string(w.Where(c => !char.IsPunctuation(c)).ToArray());
                string bleep = getWordType(w);
                if (whee.Length > 0 && bleep.Length > 0)
                {
                    bar += w.Replace(whee, bleep) + " "; // getWordType(w) + " "; 
                    woo[i] = bleep;
                }
            }
            bar = bar.Trim();
            string subj = "";

            // Check if an imperative sentence with the implied subject 'you'
            // Else, check for subject the normal way...
            if (woo.Count(p => p == "Noun") == 1 && woo.Count(p => p == "Pronoun") == 0)
            {
                subj = foo[Array.FindIndex(woo, a => a == "Noun")];
            }
            else
            {
                for (int i = 0; i < woo.Length; i++)
                {
                    if (woo[i] == "Noun" || (woo[i] == "Pronoun" && (woo.Count(p => p == "Noun") == 0)))
                    {
                        for (int ii = i; ii < woo.Length; ii++)
                        {
                            if (ii > i + 5) { break; }
                            if (woo[ii] == "Verb") { subj = foo[i]; break; }
                        }
                        if (subj != "") { break; }
                    }
                }
                // If still a failure...
                if (subj == "") 
                {
                    for (int i = 0; i < woo.Length; i++)
                    {
                        if (woo[i] == "Pronoun")
                        {
                            for (int ii = i; ii < woo.Length; ii++)
                            {
                                if (ii > i + 5) { break; }
                                if (woo[ii] == "Verb") { subj = foo[i]; break; }
                            }
                            if (subj != "") { break; }
                        }
                    }
                }
            }

            // Anaphoric expressions.
            if (subj != "")
            {
                for (int i = 0; i < foo.Length; i++)
                {
                    // Pronouns are replaced by the subject in successive bits? (No)
                    //if (foo[i] == "it" || foo[i] == "you" || foo[i] == "thou" || foo[i] == "we" || foo[i] == "they" || foo[i] == "them") { foo[i] = subj; }
                }
            }

            string finished = "";
            for (int i = 0; i < foo.Length; i++) {finished += foo[i] + " ";}
            finished = finished.Trim();
            MessageBox.Show("Subject/s: " + subj + "\nKeywords: " + keys + "\n\n\"" + finished + "\"\n\n(" + bar + ")");
            MessageBox.Show((s.wordsKnown.Count - wordsknown).ToString() + " words learned.");
        }
        private void debugDivideCompound_Click(object sender, EventArgs e)
        {
            LearnString(debugQuestionBox.Text);
        }
        private void button3_Click_1(object sender, EventArgs e)
        {
            /*
            string[] tokens = tokenizer.Tokenize(debugQuestionBox.Text);
            string[] tags = posTagger.Tag(tokens);
            string foo = "";
            for (int i = 0; i < tags.Length; i++) { foo += tokens[i] + "/" + tags[i] + " "; }
            MessageBox.Show(foo);
            */
        }

        private void tweetButton1_Click(object sender, EventArgs e)
        {
            Tweet(debugQuestionBox.Text);
        }

        private void tabPage4_Click(object sender, EventArgs e)
        {

        }

        private void routineComboBox1_DropDown(object sender, EventArgs e)
        {
            List<string> foo = new List<string>();
            foreach (botRoutines bar in l.routines) { foo.Add(bar.name); }
            routineComboBox1.DataSource = foo; // Populates it with the routines by name.
        }
        private void routineComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            List<string> foo = new List<string>();
            int la = routineComboBox1.SelectedIndex;

            //routineName.Text = "";
            routineList.DataSource = new List<string>();
            loopRoutine.Checked = false;

            if (la >= 0 && l.routines.Count > la)
            {
                botRoutines whee = l.routines[la];
                loopRoutine.Checked = whee.looped;
                for (int i = 0; i < whee.routineLocs.Count; i++)
                {
                    string bar = "";
                    if (whee.routineLocs[i] != Vector3.Zero)
                        if (i == 0 || (i > 0 && whee.routineLocs[i] != whee.routineLocs[i-1]))
                            bar += "Go to " + whee.routineLocs[i].ToString() + "; ";
                    if (whee.routinePauses[i] != 0)
                        bar += "Wait " + whee.routinePauses[i].ToString() + " seconds; ";
                    if (whee.routineAnimations[i] != UUID.Zero)
                        bar += "Play animation " + whee.routineAnimations[i].ToString() + "; ";
                    if (whee.routineSpeech[i] != "")
                        bar += "Say/emote '" + whee.routineSpeech[i] + "'; ";
                    if (bar == "") { bar = "Blank"; }
                    foo.Add(bar);
                }
                routineName.Text = whee.name;
                routineList.DataSource = foo; // Populates it with the routines by name.
            }

            List<string> botnamelist = new List<string>();
            foreach (botSettings b in l.bots) { botnamelist.Add(b.name); }
            botsListRoutines.DataSource = botnamelist;

            if (la != -1)
            {
                int sel = l.routines[la]._id;
                for (int i = 0; i < l.bots.Count; i++)
                {
                    if (l.bots[i].allowedroutines.Contains(sel))
                    {
                        botsListRoutines.SetSelected(i, true);
                    }
                    else { botsListRoutines.SetSelected(i, false); }
                }
            }
            else { for (int i = 0; i < botsListRoutines.Items.Count; i++) { botsListRoutines.SetSelected(i, false); } }
        }
        private void newRoutineButton_Click(object sender, EventArgs e)
        {
            botRoutines foo = new botRoutines();
            l.routines.Add(foo);
            foo._id = l.routines.Count;
            foo.name = "Routine " + foo._id;
        }
        private void routineName_TextChanged(object sender, EventArgs e)
        {
            l.routines[routineComboBox1.SelectedIndex].name = routineName.Text;
        }
        private void addRoutineStageButton_Click(object sender, EventArgs e)
        {
            int la = routineComboBox1.SelectedIndex;
            if (la >= 0 && la < l.routines.Count)
            {
                botRoutines foo = l.routines[la];
                routineName.Text = foo.name;
                foo.routineLocs.Add(Vector3.Zero);
                foo.routineAnimations.Add(UUID.Zero);
                foo.routinePauses.Add(0);
                foo.routineSpeech.Add("");

                List<string> aa = new List<string>();
                la = routineComboBox1.SelectedIndex;
                botRoutines whee = l.routines[la];
                for (int i = 0; i < whee.routineLocs.Count; i++)
                {
                    string bar = "";
                    if (whee.routineLocs[i] != Vector3.Zero)
                        if (i == 0 || (i > 0 && whee.routineLocs[i] != whee.routineLocs[i - 1]))
                            bar += "Go to " + whee.routineLocs[i].ToString() + "; ";
                    if (whee.routinePauses[i] != 0)
                        bar += "Wait " + whee.routinePauses[i].ToString() + " seconds; ";
                    if (whee.routineAnimations[i] != UUID.Zero)
                        bar += "Play animation " + whee.routineAnimations[i].ToString() + "; ";
                    if (whee.routineSpeech[i] != "")
                        bar += "Say/emote '" + whee.routineSpeech[i] + "'; ";
                    if (bar == "") { bar = "Blank"; }
                    aa.Add(bar);
                }
                routineList.DataSource = aa; // Populates it with the routines by name.
                routineList.SelectedIndex = whee.routineLocs.Count-1;
            }
        }
        private void delRoutineStageButton_Click(object sender, EventArgs e)
        {
            int la = routineComboBox1.SelectedIndex;
            l.routines[la].routineLocs.RemoveAt(routineList.SelectedIndex);
            l.routines[la].routinePauses.RemoveAt(routineList.SelectedIndex);
            l.routines[la].routineAnimations.RemoveAt(routineList.SelectedIndex);
            l.routines[la].routineSpeech.RemoveAt(routineList.SelectedIndex);
            int aaa = routineList.SelectedIndex;

            List<string> aa = new List<string>();
            botRoutines whee = l.routines[la];
            for (int i = 0; i < whee.routineLocs.Count; i++)
            {
                string bar = "";
                if (whee.routineLocs[i] != Vector3.Zero)
                    if (i == 0 || (i > 0 && whee.routineLocs[i] != whee.routineLocs[i - 1]))
                        bar += "Go to " + whee.routineLocs[i].ToString() + "; ";
                if (whee.routinePauses[i] != 0)
                    bar += "Wait " + whee.routinePauses[i].ToString() + " seconds; ";
                if (whee.routineAnimations[i] != UUID.Zero)
                    bar += "Play animation " + whee.routineAnimations[i].ToString() + "; ";
                if (whee.routineSpeech[i] != "")
                    bar += "Say/emote '" + whee.routineSpeech[i] + "'; ";
                if (bar == "") { bar = "Blank"; }
                aa.Add(bar);
            }
            routineList.DataSource = aa; // Populates it with the routines by name.

            if (aaa < whee.routineLocs.Count) { routineList.SelectedIndex = aaa; }
            else { routineList.SelectedIndex = aaa - 1; }
        }
        private void routineList_SelectedIndexChanged(object sender, EventArgs e)
        {
            botRoutines foo = l.routines[routineComboBox1.SelectedIndex];
            int bar = routineList.SelectedIndex;

            if (routineList.SelectedIndex >= 0 && routineList.SelectedIndex < foo.routineLocs.Count)
            {
                gotoEntry1.Text = foo.routineLocs[bar].ToString();
                waitforEntry1.Text = foo.routinePauses[bar].ToString();
                sayEntry1.Text = foo.routineSpeech[bar];
            }
        }
        private void applyRoutineChangeButton_Click(object sender, EventArgs e)
        {
            botRoutines foo = l.routines[routineComboBox1.SelectedIndex];
            int bar = routineList.SelectedIndex;

            try
            {
                foo.routineLocs[bar] = Vector3.Parse(gotoEntry1.Text);
                foo.routinePauses[bar] = int.Parse(waitforEntry1.Text);
                foo.routineSpeech[bar] = sayEntry1.Text;
            }
            catch { }

            List<string> aa = new List<string>();
            int la = routineComboBox1.SelectedIndex;
            botRoutines whee = l.routines[la];
            for (int i = 0; i < whee.routineLocs.Count; i++)
            {
                string bb = "";
                if (whee.routineLocs[i] != Vector3.Zero)
                    if (i == 0 || (i > 0 && whee.routineLocs[i] != whee.routineLocs[i - 1]))
                        bb += "Go to " + whee.routineLocs[i].ToString() + "; ";
                if (whee.routinePauses[i] != 0)
                    bb += "Wait " + whee.routinePauses[i].ToString() + " seconds; ";
                if (whee.routineAnimations[i] != UUID.Zero)
                    bb += "Play animation " + whee.routineAnimations[i].ToString() + "; ";
                if (whee.routineSpeech[i] != "")
                    bb += "Say/emote '" + whee.routineSpeech[i] + "'; ";
                if (bb == "") { bb = "Blank"; }
                aa.Add(bb);
            }
            routineList.DataSource = aa; // Populates it with the routines by name.
        }
        private void loopRoutine_CheckedChanged(object sender, EventArgs e)
        {
            botRoutines foo = l.routines[routineComboBox1.SelectedIndex];
            foo.looped = loopRoutine.Checked;
        }
        private void moveRoutine1_Click(object sender, EventArgs e)
        {
            // Move up
            int la = routineComboBox1.SelectedIndex;
            int la2 = routineList.SelectedIndex;
            if (la2 == 0) { return; }
            botRoutines whee = l.routines[la];
            SwapItems(whee.routineLocs, la2, la2 - 1);
            SwapItems(whee.routinePauses, la2, la2 - 1);
            SwapItems(whee.routineSpeech, la2, la2 - 1);

            List<string> foo = new List<string>();
            if (la >= 0 && l.routines.Count > la)
            {
                whee = l.routines[la];
                loopRoutine.Checked = whee.looped;
                for (int i = 0; i < whee.routineLocs.Count; i++)
                {
                    string bar = "";
                    if (whee.routineLocs[i] != Vector3.Zero)
                        if (i == 0 || (i > 0 && whee.routineLocs[i] != whee.routineLocs[i - 1]))
                            bar += "Go to " + whee.routineLocs[i].ToString() + "; ";
                    if (whee.routinePauses[i] != 0)
                        bar += "Wait " + whee.routinePauses[i].ToString() + " seconds; ";
                    if (whee.routineAnimations[i] != UUID.Zero)
                        bar += "Play animation " + whee.routineAnimations[i].ToString() + "; ";
                    if (whee.routineSpeech[i] != "")
                        bar += "Say/emote '" + whee.routineSpeech[i] + "'; ";
                    if (bar == "") { bar = "Blank"; }
                    foo.Add(bar);
                }
                routineList.DataSource = foo; // Populates it with the routines by name.
                routineList.SelectedIndex = la2 - 1;
            }
        }
        private void moveRoutine2_Click(object sender, EventArgs e)
        {
            // Move down
            int la = routineComboBox1.SelectedIndex;
            int la2 = routineList.SelectedIndex;
            if (la2 == l.routines[la].routineLocs.Count-1) { return; }
            botRoutines whee = l.routines[la];
            SwapItems(whee.routineLocs, la2, la2 + 1);
            SwapItems(whee.routinePauses, la2, la2 + 1);
            SwapItems(whee.routineSpeech, la2, la2 + 1);            

            List<string> foo = new List<string>();
            if (la >= 0 && l.routines.Count > la)
            {
                whee = l.routines[la];
                loopRoutine.Checked = whee.looped;
                for (int i = 0; i < whee.routineLocs.Count; i++)
                {
                    string bar = "";
                    if (whee.routineLocs[i] != Vector3.Zero)
                        if (i == 0 || (i > 0 && whee.routineLocs[i] != whee.routineLocs[i - 1]))
                            bar += "Go to " + whee.routineLocs[i].ToString() + "; ";
                    if (whee.routinePauses[i] != 0)
                        bar += "Wait " + whee.routinePauses[i].ToString() + " seconds; ";
                    if (whee.routineAnimations[i] != UUID.Zero)
                        bar += "Play animation " + whee.routineAnimations[i].ToString() + "; ";
                    if (whee.routineSpeech[i] != "")
                        bar += "Say/emote '" + whee.routineSpeech[i] + "'; ";
                    if (bar == "") { bar = "Blank"; }
                    foo.Add(bar);
                }
                routineList.DataSource = foo; // Populates it with the routines by name.
                routineList.SelectedIndex = la2+1;
            }
        }

        private void tourComboBox_DragDrop(object sender, DragEventArgs e)
        {

        }
        private void tourComboBox_DropDown(object sender, EventArgs e)
        {
            List<string> foo = new List<string>();
            foreach (botTours bar in l.tours) { string aa = ""; if (bar.enabled) { aa = "~ "; } foo.Add(aa + bar.tourName); }
            tourComboBox.DataSource = foo; // Populates it with the tours by name.
        }
        private void tourComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshTourList();
            int la = tourComboBox.SelectedIndex;
            if (la != -1)
            {
                int sel = l.tours[la]._id;
                for (int i = 0; i < l.bots.Count; i++)
                {
                    if (l.bots[i].allowedtours.Contains(sel))
                    {
                        botsListTours.SetSelected(i, true);
                    }
                    else { botsListTours.SetSelected(i, false); }
                }
            }
            else { for (int i = 0; i < botsListTours.Items.Count; i++) { botsListTours.SetSelected(i, false); } }
        }
        private void newTourButton_Click(object sender, EventArgs e)
        {
            botTours foo = new botTours();
            l.tours.Add(foo);
            foo._id = l.tours.Count;
            foo.tourName = "Tour " + foo._id;
        }
        private void enableTour_CheckedChanged(object sender, EventArgs e)
        {
            l.tours[tourComboBox.SelectedIndex].enabled = enableTour.Checked;
        }
        private void tourName_TextChanged(object sender, EventArgs e)
        {
            l.tours[tourComboBox.SelectedIndex].tourName = tourName.Text;
        }
        private void tourStageText_TextChanged(object sender, EventArgs e)
        {
            //l.tours[tourComboBox.SelectedIndex].tourInfo[tourSpotList.SelectedIndex][tourStageList.SelectedIndex] = tourStageText.Text;
        }
        private void applyTourChanges_Click(object sender, EventArgs e)
        {
            string[] foo = tourStageSpeech.Text.Split(new string[]{Environment.NewLine}, new StringSplitOptions());
            int la = tourComboBox.SelectedIndex;
            l.tours[tourComboBox.SelectedIndex].tourInfo[la].Clear();
            for (int i = 0; i < foo.Count(); i++)
            {
                l.tours[tourComboBox.SelectedIndex].tourInfo[la].Add(foo[i]);
            }
            RefreshTourList();
        }
        private void tourList_SelectedIndexChanged(object sender, EventArgs e)
        {
            for (int i = tourList.SelectedIndex; i > -1; i--) {
                if (!tourList.Items[i].ToString().StartsWith("- ")) {
                    int foo = 0;
                    for (int ii = 0; ii <= i; ii++) { if (!tourList.Items[ii].ToString().StartsWith(" ")) { foo++; } }
                    label6.Text = "Currently Selected: Stop #" + foo;
                    tourLocEdit.Text = l.tours[tourComboBox.SelectedIndex].tourLocs[foo - 1].ToString();
                    string bar = "";
                    for (int ii = 0; ii < l.tours[tourComboBox.SelectedIndex].tourInfo[foo-1].Count; ii++) {
                        bar += l.tours[tourComboBox.SelectedIndex].tourInfo[foo - 1][ii] + Environment.NewLine;
                    }
                    bar = bar.Trim();
                    tourStageSpeech.Text = bar;
                    tourstageedit = foo - 1;
                    break; 
                }
            }
        }
        private void button4_Click(object sender, EventArgs e)
        {
            Vector3 foo;
            if (!Vector3.TryParse(tourLocEdit.Text, out foo)) { MessageBox.Show("Invalid Coordinates"); }
            else { l.tours[tourComboBox.SelectedIndex].tourLocs[tourstageedit] = foo; RefreshTourList(); }
        }
        private void RefreshTourList()
        {
            int ind = tourList.SelectedIndex;
            List<string> foo = new List<string>();
            int la = tourComboBox.SelectedIndex;
            botTours whee = l.tours[la];
            for (int i = 0; i < whee.tourLocs.Count; i++)
            {
                foo.Add("Go to " + whee.tourLocs[i].ToString());
                for (int ii = 0; ii < whee.tourInfo[i].Count; ii++)
                {
                    foo.Add("  Say: " + whee.tourInfo[i][ii]);
                }
            }
            tourList.DataSource = foo; // Populates it with the routines by name.
            tourName.Text = whee.tourName;
            enableTour.Checked = whee.enabled;
            try { tourList.SelectedIndex = ind; }
            catch { tourList.SelectedIndex = -1; }
        }
        private void addTourSpot_Click(object sender, EventArgs e)
        {
            int la = tourComboBox.SelectedIndex;
            l.tours[la].tourLocs.Add(Vector3.Zero);
            l.tours[la].tourInfo.Add(new List<string>());
            RefreshTourList();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (s.privateaccess) { s.privateaccess = false; }
            else { s.privateaccess = true; }
        }
        private void autoTweetAllow_CheckedChanged(object sender, EventArgs e)
        {
            s.autotweet = autoTweetAllow.Checked;
        }
        private void tweetStoryButton_Click(object sender, EventArgs e)
        {
            if (l.stories[storyList1.SelectedIndex].tweeted == false) { string txt = ""; foreach (string st in l.stories[storyList1.SelectedIndex].paragraphs) { txt += st + " "; }; LongTweet(txt.Trim()); l.stories[storyList1.SelectedIndex].tweeted = true; }
            else { MessageBox.Show("Already tweeted."); }
        }
        private void label9_Click(object sender, EventArgs e)
        {

        }
        private void label11_Click(object sender, EventArgs e)
        {

        }
        private void button5_Click(object sender, EventArgs e)
        {
            botSettings newbot = new botSettings();
            l.bots.Add(newbot);
            foreach (textNode t in l.nodes) { newbot.allowednodes.Add(t._id); } // By default, allow all.
            foreach (botStories s in l.stories) { newbot.allowedstories.Add(s._id); } // By default, allow all.
            foreach (botRoutines r in l.routines) { newbot.allowedroutines.Add(r._id); } // By default, allow all.
            foreach (botTours t in l.tours) { newbot.allowedtours.Add(t._id); } // By default, allow all.

            if (newbot.speech.Count == 0)
            {
                newbot.speechnames.Add("Default");
                defaultSpeechAdd(newbot);
            }
            if (newbot.sbindings.Count <= 0)
            {
                newbot.sbindings.Add("Greeting");
                newbot.sbindings.Add("Ask to follow");
                newbot.sbindings.Add("Confirm follow");
                newbot.sbindings.Add("Deny follow");
                newbot.sbindings.Add("Asked to follow; already following");
                newbot.sbindings.Add("Asked to stop following");
                newbot.sbindings.Add("Askd to stop following; not following");
                newbot.sbindings.Add("Ask for a story");
                newbot.sbindings.Add("Hearing story");
                newbot.sbindings.Add("Hearing story 2");
                newbot.sbindings.Add("Hearing story 3");
                newbot.sbindings.Add("Hearing story; ask if over");
                newbot.sbindings.Add("Hearing story; story over");
                newbot.sbindings.Add("Hearing story; story not over");
                newbot.sbindings.Add("Tell story; success");
                newbot.sbindings.Add("Tell story; no stories");
                newbot.sbindings.Add("Telling story; story over");
                newbot.sbindings.Add("Go to tour location");
                newbot.sbindings.Add("Tourist teleport");
                newbot.sbindings.Add("Tour end");
                newbot.sbindings.Add("Reached tour spot; begin explanation");
                newbot.sbindings.Add("Reached tour spot; greet latecomer tourist; begin explanation");
                newbot.sbindings.Add("Tourist late; ask to stop tour");
                newbot.sbindings.Add("Tourist late; don't stop");
                newbot.sbindings.Add("Tourist late; stop");
                newbot.sbindings.Add("End tour spot; more to go");
                newbot.sbindings.Add("End tour spot; last spot");
                newbot.sbindings.Add("Stop tour");
                newbot.sbindings.Add("Stop tour; wasn't touring");
                newbot.sbindings.Add("Flood detected");
                newbot.sbindings.Add("Ask to teleport");
                newbot.sbindings.Add("Asked to start learning tour");
                newbot.sbindings.Add("Point out tourist spot");
                newbot.sbindings.Add("Hear story about tourist spot");
                newbot.sbindings.Add("Hear story about tourist spot 2");
                newbot.sbindings.Add("Hear story about tourist spot 3");
                newbot.sbindings.Add("Stop teaching");
                newbot.sbindings.Add("Stop teaching; wasn't learning");
                newbot.sbindings.Add("Not on access list");
                newbot.sbindings.Add("Doesn't have response");
                newbot.sbindings.Add("Happy exclamation");
                newbot.sbindings.Add("Happy exclamation 2");
                newbot.sbindings.Add("'All right'; spoken");
                newbot.sbindings.Add("'All right'; exclamation");
                newbot.sbindings.Add("Rejected 1");
                newbot.sbindings.Add("Rejected 2"); // 45
                newbot.sbindings.Add("Sad exclamation");
                newbot.sbindings.Add("Thank you");
                newbot.sbindings.Add("");
                newbot.sbindings.Add("");
                newbot.sbindings.Add("");
                newbot.sbindings.Add(""); // 50
                newbot.sbindings.Add("");
                newbot.sbindings.Add("");
                newbot.sbindings.Add("");
                newbot.sbindings.Add(""); // 55
                newbot.sbindings.Add("");
                newbot.sbindings.Add("");
                newbot.sbindings.Add("");
                newbot.sbindings.Add("");
                newbot.sbindings.Add("Ask to tell story"); // 60
                newbot.sbindings.Add("Hitchhiking; reached destination");
                newbot.sbindings.Add("Hitchhiking; farewell");
                newbot.sbindings.Add("Hitchhiking; Ask to finish story");
                newbot.sbindings.Add("Sees person; waves");
                newbot.sbindings.Add("Person passes by"); // 65
                newbot.sbindings.Add("Tell where going");
                newbot.sbindings.Add("Tell where going; city only");
                newbot.sbindings.Add("Note city");
                newbot.sbindings.Add("Greeting a known person");
                newbot.sbindings.Add("Ask to give tour"); // 70 
                newbot.sbindings.Add("Tour refused");
                newbot.sbindings.Add("No tours available");
                newbot.sbindings.Add("Asked to stop following; wasn't");
                newbot.sbindings.Add("Asked to stop talking");
                newbot.sbindings.Add("Tweeted"); // 75
                newbot.sbindings.Add("Can't tweet; too long");
                newbot.sbindings.Add("Weather");
                newbot.sbindings.Add("Ask to share story");
                newbot.sbindings.Add("");
                newbot.sbindings.Add("Improper command"); // 80
            }

            List<string> botnamelist = new List<string>();
            foreach (botSettings b in l.bots) { botnamelist.Add(b.name); }
            botListBox1.DataSource = botnamelist;
            botListBox1.SelectedIndex = l.bots.Count - 1;
            botsListNodes.DataSource = botnamelist;
            botsListStories.DataSource = botnamelist;
            botsListTours.DataSource = botnamelist;
            botsListRoutines.DataSource = botnamelist;
        }
        private void tabPage3_Click(object sender, EventArgs e)
        {

        }
        private void botNameDescUpdate_Click(object sender, EventArgs e)
        {
            s.name = botName.Text;
            s.description = botDescription.Text;
            int foo = botListBox1.SelectedIndex;

            List<string> botnamelist = new List<string>();
            foreach (botSettings b in l.bots) { botnamelist.Add(b.name); }
            botListBox1.DataSource = botnamelist;

            botListBox1.SelectedIndex = foo;

            accessList1.DataSource = s.alist;

            speechSettings.DataSource = s.speechnames;
            speechList.DataSource = s.sbindings;
        }
        private void gridSelection_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (gridSelection.SelectedIndex == 0) { Client.Settings.LOGIN_SERVER = "http://emLAB0026.sharcnet.ca:8002"; }
            else if (gridSelection.SelectedIndex == 1) { Client.Settings.LOGIN_SERVER = "http://199.241.161.254:9000/"; }
            else if (gridSelection.SelectedIndex == 2) { Client.Settings.LOGIN_SERVER = "https://login.agni.lindenlab.com/cgi-bin/login.cgi"; }
            else if (gridSelection.SelectedIndex == 3) { Client.Settings.LOGIN_SERVER = "http://127.0.0.1:9000"; }
            else if (gridSelection.SelectedIndex == 4) { Client.Settings.LOGIN_SERVER = s.customLogin; }
        }
        private void label10_Click(object sender, EventArgs e)
        {

        }
        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            List<string> botnamelist = new List<string>();
            foreach (botSettings b in l.bots) { botnamelist.Add(b.name); }
            botsListNodes.DataSource = botnamelist;
            botsListStories.DataSource = botnamelist;
            botsListTours.DataSource = botnamelist;
            botsListRoutines.DataSource = botnamelist;
        }
        private void button6_Click(object sender, EventArgs e)
        {
            foreach(int i in botsListNodes.SelectedIndices) {}
        }

        private void botListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            s = l.bots[botListBox1.SelectedIndex];
            botName.Text = s.name;
            botDescription.Text = s.description;
        }
        
        private void refreshBotsNodes()
        {
            if (nodesel != -1 && nodesel < l.nodes.Count)
            {
                for (int i = 0; i < l.bots.Count; i++)
                {
                    if (botsListNodes.SelectedIndices.Contains(i))
                    {
                        if (!l.bots[i].allowednodes.Contains(nodesel)) { l.bots[i].allowednodes.Add(nodesel); }
                    }
                    else if (l.bots[i].allowednodes.Contains(nodesel)) { l.bots[i].allowednodes.RemoveAll(x => (x == nodesel)); }
                }
            }
            //else { for (int i = 0; i < botsListNodes.Items.Count; i++) { botsListNodes.SetSelected(i, false); } }
        }
        private void refreshBotsStories()
        {
            int ind = storyList1.SelectedIndex;
            if (ind != -1 && ind < l.stories.Count)
            {
                int sel = l.stories[ind]._id;
                for (int i = 0; i < l.bots.Count; i++)
                {
                    if (botsListStories.SelectedIndices.Contains(i))
                    {
                        if (!l.bots[i].allowedstories.Contains(sel)) { l.bots[i].allowedstories.Add(sel); }
                    }
                    else if (l.bots[i].allowedstories.Contains(sel)) { l.bots[i].allowedstories.RemoveAll(x => (x == sel)); }
                }
            }
            else { for (int i = 0; i < botsListStories.Items.Count; i++) { botsListStories.SetSelected(i, false); } }
        }
        private void refreshBotsRoutines()
        {
            int ind = routineComboBox1.SelectedIndex;
            if (ind != -1 && ind < l.routines.Count)
            {
                int sel = l.routines[ind]._id;
                for (int i = 0; i < l.bots.Count; i++)
                {
                    if (botsListRoutines.SelectedIndices.Contains(i))
                    {
                        if (!l.bots[i].allowedroutines.Contains(sel)) { l.bots[i].allowedroutines.Add(sel); }
                    }
                    else if (l.bots[i].allowedroutines.Contains(sel)) { l.bots[i].allowedroutines.RemoveAll(x => (x == sel)); }
                }
            }
            else { for (int i = 0; i < botsListRoutines.Items.Count; i++) { botsListRoutines.SetSelected(i, false); } }
        }
        private void refreshBotsTours()
        {
            int sel = tourComboBox.SelectedIndex;
            if (sel != -1 && sel < l.tours.Count)
            {
                int selectedtour = l.tours[sel]._id;
                for (int i = 0; i < l.bots.Count; i++)
                {
                    if (botsListTours.SelectedIndices.Contains(i))
                    {
                        if (!l.bots[i].allowedtours.Contains(selectedtour)) { l.bots[i].allowedtours.Add(selectedtour); }
                    }
                    else if (l.bots[i].allowedtours.Contains(selectedtour)) { l.bots[i].allowedtours.RemoveAll(x => (x == selectedtour)); }
                }
            }
            else { for (int i = 0; i < botsListTours.Items.Count; i++) { botsListTours.SetSelected(i, false); } }
        }


        private void botsListNodes_Click(object sender, EventArgs e)
        {
            refreshBotsNodes();
        }
        private void botsListStories_Click(object sender, EventArgs e)
        {
            refreshBotsStories();
        }
        private void botsListRoutines_Click(object sender, EventArgs e)
        {
            refreshBotsRoutines();
        }
        private void botsListTours_Click(object sender, EventArgs e)
        {
            refreshBotsTours();
        }

        private void botsListNodes_SelectedIndexChanged(object sender, EventArgs e)
        {
        }
        private void botsListNodes_MouseClick(object sender, MouseEventArgs e)
        {
        }
        private void botsListTours_MouseDown(object sender, MouseEventArgs e)
        {
        }
        private void botsListRoutines_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
        private void botsListTours_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void deleteBotButton_Click(object sender, EventArgs e)
        {
            DialogResult foo =  MessageBox.Show("Delete the entire bot? You sure?", "MockingBOT", MessageBoxButtons.YesNo);
            if (foo == DialogResult.Yes) {
                l.bots.Remove(s);
                List<string> botnamelist = new List<string>();
                foreach (botSettings b in l.bots) { botnamelist.Add(b.name); }
                botListBox1.DataSource = botnamelist;
            }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            speakingaloud = checkBox2.Checked;
        }

        private void loginURL_TextChanged(object sender, EventArgs e)
        {
            gridSelection.SelectedIndex = 4;
            s.customLogin = loginURL.Text;
            Client.Settings.LOGIN_SERVER = s.customLogin;
        }

    }
}
