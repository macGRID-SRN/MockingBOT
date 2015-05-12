//standard .NET references
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;
using System.Configuration;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.Serialization;
using System.Speech.Synthesis;

//OpenSim references
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework;
using OpenSim.Region.CoreModules;
using OpenSim.Region.OptionalModules;
using OpenSim.Region.OptionalModules.World.NPC;
using OpenSim.Server.Base;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenNLP;
using OpenNLP.Tools.Tokenize;
using OpenNLP.Tools.PosTagger;
using OpenNLP.Tools.NameFind;
using OpenNLP.Tools.Parser;
using OpenNLP.Tools.Chunker;

// Etc
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LinqToTwitter;

//required for newer versions of Mono
[assembly: Addin("GPSAvatarWithSocketModule", "0.1")]
[assembly: AddinDependency("OpenSim", "0.5")]

namespace MockingBOT
{

    //makes the module visible to OpenSim's module mechanism (Mono.Addins)
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "MockingBOT")]

    /*
     * First road toward a Region Module that moves an object according to incoming GPS co-ordinates. This particular
     * class simply creates a single object per Region & then moves it in one direction on every tick of a timer.
     */
    public class MockingBOT : ISharedRegionModule
    {

        //can be op.used to output messages both to the console & the OpenSim.log file
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        //OpenMetaverse.UUID CreateNPC(string firstname, string lastname, OpenMetaverse.Vector3 position, OpenMetaverse.UUID owner, bool senseAsAgent, OpenSim.Region.Framework.Scenes.Scene scene, OpenSim.Framework.AvatarAppearance appearance);
        
        /*
         * A Scene is OpenSim's representation of the contents of a region. As this is a shared region module there may be
         * many regions & thus many Scenes so we need a List. However if this were a non-shared region module there would be
         * only one Scene so we wouldn't need a list.
         * 
         * A Dictionary is like a Hashtable, in this instance op.used to associate Scenes with collections of prims.
         */

        private static BotStorage l = new BotStorage(); // The actual file
        private static botSettings s = new botSettings(); // The bot loaded into memory. Kinda useless - further optimization would probably remove this.
        private static botOperator op = new botOperator(); // This class contains all the miscellaneous variables bots use in typical operations, but need to be reserved for each bot for the sake of running them individually.

        private static AvatarAppearance _defaultappearance = new AvatarAppearance(); // The default appearance for loaded bots. Borked at the moment and doesn't do anything.

        public static List<Scene> m_scenes = new List<Scene>(); // List of scenes.
        public static SceneManager scenes = new SceneManager(); // Scene Manager, with its own list of scenes. Wow, I sure didn't know what I was doing when I first started working with this thing!

        int time = 0; // time for the OnTick() method
        public static bool tts = false; // This is purely for whether or not we want text-to-speech. 
        // Most likely, though, Mono won't let me export this at all with tts allowed, since the code itself is Windows-only.
        public static SpeechSynthesizer Speech = new SpeechSynthesizer(); // This thing, specifically. Windows-only class.

        // Let's create us a timer. 
        System.Timers.Timer aTimer = new System.Timers.Timer();

        public static NPCModule bot = new NPCModule(); // Main NPCModule. Necessary.
        // Various things used to index bots for various functions. Sometimes you need a UUID. Sometimes you need an NPCAvatar. NPCAvatars do contain a UUID, but not vice versa (at least, not without going through some kind of maze).
        public static List<UUID> bots = new List<UUID>();
        public static List<NPCAvatar> npcavatars = new List<NPCAvatar>();
        public static List<int> npcid = new List<int>();
        public static List<botOperator> npcop = new List<botOperator>();
        
        // All connected clients. Mostly for IMing.
        public static ClientManager clients = new ClientManager();


        static Random rnd = new Random(); // For getting random numbers, for whatever reasons they might be needed.

        static int state = 0; // This might be used at some point - it can be used to determine which nodes the bot has access to, as nodes also have 'states' attached to them. Not op.used now, though.

        static string[] conjunctions = new string[] { "and", "for", "nor", "but", "or", "yet", "so" }; // For linguistics stuff. Doesn't need to be up here since it's used in only one function.

        // UNUSED linguistic models. Uses up way too much memory. Here just in case something changes.
        static string mModelPath = new System.Uri(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).LocalPath + @"\OpenNLP\Models\";
        /*
        EnglishMaximumEntropyTokenizer tokenizer = new EnglishMaximumEntropyTokenizer(mModelPath + "EnglishTok.nbin");
        EnglishMaximumEntropyPosTagger posTagger = new EnglishMaximumEntropyPosTagger(mModelPath + "EnglishPOS.nbin");
        EnglishTreebankParser parser = new EnglishTreebankParser(mModelPath, true, false);
        EnglishNameFinder nameFinder = new EnglishNameFinder(mModelPath);
        EnglishTreebankChunker chunker = new EnglishTreebankChunker(mModelPath + "EnglishChunk.nbin");
        */

        // For Twitter functions. These below are obtained from Twitter's developer's panel. Currently, it's bound a small account I made solely for debugging ('Senor Bottikins'). 
        // You can switch it to KulturBOT (or any other account) rather easily by changing these variables.
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

        //methods required by IRegionModuleBase (which ISharedRegionModule & INonSharedRegionModule extend)
        /*
         * This name is shown when the console command "show modules" is run.
         */
        public string Name { get { return "MockingBOTRegionModule"; } }
        /*
         * If this is not null, then the module is not loaded if any other module implements the given interface.
         * One use for this is to provide 'stub' functionality implementations that are only active if no other
         * module is present.
         */
        public Type ReplaceableInterface { get { return null; } }
        /*
         * This method is called immediately after the region module has been loaded into the runtime, before it
         * has been added to a scene or scenes. IConfigSource is a Nini class that contains the concatentation of
         * config parameters from OpenSim.ini, OpenSimDefaults.ini and the appropriate ini files in bin/config-include
         */
        public void Initialise(IConfigSource source)
        {
            m_log.DebugFormat("[MockingBOT]: Initialising.");
            LoadSettings(Environment.CurrentDirectory + "\\botsettings.xml");

            // To be used for 'OnTick2()' which basically runs the show.
            aTimer.Elapsed += new ElapsedEventHandler(OnTick2);
            aTimer.Interval = 20; // 1000 is a second, so this is pretty much all the time. However, it only does things every 10, 20, and 30 ticks.
            aTimer.Enabled = true; // Duh.

            Speech.SelectVoiceByHints(VoiceGender.Male, VoiceAge.Senior);
            foreach (var v in Speech.GetInstalledVoices().Select(v => v.VoiceInfo))
            {
                m_log.DebugFormat("Name:{0}, Gender:{1}, Age:{2}",
                v.Description, v.Gender, v.Age);
            }
        }
        /*
         * This method is called when a region is added to the module. For shared modules this will happen multiple
         * times (one for each module). For non-shared modules this will happen only once. The module can store the
         * scene reference and use it later to reach and invoke OpenSim internals and interfaces.
         */
        public void AddRegion(Scene scene)
        {
            m_log.DebugFormat("[MockingBOT]: AddRegion() - adding region...", scene.RegionInfo.RegionName);

            //add the scene reference for this region to the List of scenes
            m_scenes.Add(scene);
            scenes.Add(scene);
            scene.EventManager.OnFrame += new EventManager.OnFrameDelegate(OnTick);
            scene.EventManager.OnChatToClients += new EventManager.ChatToClientsEvent(OnChatToClients);
            scene.EventManager.OnNewClient +=new EventManager.OnNewClientDelegate(OnNewClient);

            m_log.DebugFormat("[MockingBOT]: AddRegion() - region added: {0}", scene.RegionInfo.RegionName);
        }
        /*
         * Called when a region is removed from a module. For shared modules this can happen multiple times. For
         * non-shared region modules this will happen only once and should shortly be followed by a Close(). On
         * simulator shutdown, this method will be called before Close(). RemoveRegion() can also be called if a
         * region/scene is manually removed while the simulator is running.
         */
        public void RemoveRegion(Scene scene)
        {
            m_log.DebugFormat("[MockingBOT]: RemoveRegion()", scene.RegionInfo.RegionName);
        }
        /*
         * Called when all modules have been added for a particular scene/region. Since all other modules are now
         * loaded, this gives the module an opportunity to obtain interfaces or subscribe to events on other modules.
         * Called once for a non-shared region module and multiple times for shared region modules.
         */
        public void RegionLoaded(Scene scene)
        {
        }
        // Whenever a new client has entered the grid...
        public void OnNewClient(IClientAPI client)
        {
            client.OnInstantMessage += new ImprovedInstantMessage(OnInstantMessage);
            clients.Add(client);
        }
        /*
         * This method will be invoked when the sim is closing down.
         */
        public void Close()
        {
            m_log.DebugFormat("[MockingBOT]: Close() - not implemented.");
        }
        // method required by ISharedRegionModule        
        public void PostInitialise()
        {
            m_log.DebugFormat("[MockingBOT]: PostInitialise() - done.");
        }

        // This runs every step, activated by OpenSim's own framework (and thus required)...
        private void OnTick()
        { 
        }
        // ...But I'm not using it. This method down here runs on a timer, created above. 
        // The reason is that if THIS method gets paused, it won't pause the entire sim. Which I do often, to give the bot natural pauses, wait for other events, etc.
        private void OnTick2(object source, ElapsedEventArgs e) 
        {
            time++;
            if (time > 32000) {time = 0;} // Just to prevent it hitting any kind of limit. 32000 is a pretty low ceiling.

            if (time % 10 == 0) // Every ten steps...
            {
                for (int bi = 0; bi < npcid.Count; bi++) // Cycle through all of the bots.
                {
                    botSettings s = l.bots[npcid[bi]]; // Get that bot's settings file...
                    NPCAvatar b = npcavatars[bi]; // ...avatar...
                    botOperator op = npcop[npcavatars.IndexOf(b)]; // ...and operator.

                    // Cycle through that bot's mLearning thing and make sure it's okay. This is literally just cleanup.
                    for (int i = 0; i < s.mLearning.Count; i++)
                    {
                        if (s.mLearning[i].Count < l.nodes.Count) // This is a problem that is easily solved.
                        {
                            // Basically we just add as many as needed so these lists are the same length. That's all that matters. Basically, we met someone knew and they haven't been accommodated for.
                            int foo = l.nodes.Count - s.mLearning[i].Count;
                            for (int ii = 0; ii < foo; ii++)
                            {
                                s.mLearning[i].Add(0); 
                            }
                        }
                    }

                    // If this bot has messaged too much...
                    if (op.flooddetect == 0 && op.timesmsgd >= 30)
                    {
                        op.flooddetect = 1;
                        m_log.DebugFormat("{0} tripped a flood detect and she's very sorry about it.", b.Name);
                    }
                    op.floodcdt--;
                    if (op.floodcdt <= 0)
                    {
                        // Reset that timer.
                        op.timesmsgd = 0;
                        op.floodcdt = op.floodcd; // floodcd is how long the countdown timer is (versus cdt which is the actual timer).
                        if (op.flooddetect != 0) { op.flooddetect = 0; m_log.DebugFormat("{0} is no longer under flood detect", b.Name); }
                    }

                    if (op.tourWait == 0) { op.senttele = false; } // If done waiting, toggle the thing that says 'you have sent a teleport request while you were waiting'.
                    if (op.storytelling == false) // Clean-up -- if not telling a story, reset the variables that say which one is being told and what stage.
                    {
                        op.storystage = 0;
                        op.storywait = 0;
                    }
                }
            }

            if (time % 20 == 0)
            {
                for (int bi = 0; bi < npcid.Count; bi++)
                {
                    botSettings s = l.bots[npcid[bi]]; // Get that bot's settings file...
                    NPCAvatar b = npcavatars[bi]; // ...avatar...
                    botOperator op = npcop[npcavatars.IndexOf(b)]; // ...and operator.

                    // Just two 'countdowns' used for other functions.
                    if (op.askWait > 0) { op.askWait--; }
                    if (op.movetimeout > 0) { op.movetimeout--; }

                    if (sP(b.AgentId).Velocity == Vector3.Zero && op.hhdest == Vector3.Zero) { randomizeDestination(); } // If hitchhiking destination hasn't been set, set it.
                    if (op.following == false) { op.followtime = 0; }

                    if (op.storytelling == true && op.waiting == false)
                    {
                        //op.storywait++;
                        op.storywait = 99999;
                        String whoop = String.Empty;
                        if (op.currentstory.paragraphs.Count > op.storystage)
                        {
                            whoop = op.currentstory.paragraphs[op.storystage];
                            int len = whoop.Split(' ').Length;
                            if (len < (op.storywait) * 0.5) // 1.5 for 90 WPM
                            {
                                //op.storywait = 0;
                                op.storystage++;
                                BotSpeak(b, whoop, false);
                            }
                            else
                            {
                            }
                        }
                        else
                        {
                            op.storytelling = false;
                            op.storystage = 0;
                            if (op.storywaituntilover) { BotSpeak(b, getSpeech(16), false); }
                            else { BotSpeak(b, getSpeech(16)); }
                        }
                    }
                    else { op.storywaituntilover = false; }

                    if (op.touring == true)
                    {
                        if (op.currenttour.tourLocs.Count > op.tourStage)
                        {
                            op.tourLoc = op.currenttour.tourLocs[op.tourStage];
                            if (Vector3.Distance(b.Position, op.tourLoc) <= 8)
                            {
                                if (op.touristPresent == false) { op.tourWait++; waitForTourist(b, op.tourist); } // op.waiting routine.
                                else // TOUR COMMENCE
                                {
                                    op.tourWait = 0;
                                    if (op.currenttour.tourInfo[op.tourStage].Count > op.tstoryStage)
                                    {
                                        //if (op.tstoryStage == 0) { Client.Self.AnimationStop(Animations.POINT_YOU, true); Client.Self.AnimationStart(Animations.POINT_YOU, true); }
                                        string woo = op.currenttour.tourInfo[op.tourStage][op.tstoryStage];
                                        op.tstoryStage++;
                                        BotSpeak(b, woo);
                                    }
                                    else if (op.currenttour.tourInfo[op.tourStage].Count == op.tstoryStage)
                                    {
                                        if (op.currenttour.tourLocs.Count > op.tourStage + 1)
                                        {
                                            op.tstoryStage++;
                                            BotSpeak(b, getSpeech(25));
                                        }
                                        else
                                        {
                                            op.tstoryStage++;
                                            BotSpeak(b, getSpeech(26));
                                        }
                                    }
                                }
                            }
                            else if (op.movetimeout <= 0)
                            {
                                goTo(b, op.tourLoc);
                                op.movetimeout = 12;
                                //Client.Self.AutoPilotLocal((int)op.tourLoc.X, (int)op.tourLoc.Y, (int)op.tourLoc.Z); // Keep putting us en route.
                            }
                        }
                        else
                        {
                            op.touring = false;
                            BotSpeak(b, getSpeech(19));
                        }
                    }
                    else if (op.listening)
                    {
                        if (op.endcheck)
                        {
                            op.listening = false;
                            BotSpeak(b, getSpeech(11));
                            if (waitForAnswer(true) == true)
                            {
                                op.listening = false;
                                BotSpeak(b, getSpeech(12));
                                DoAnimation(b.AgentId, Animations.CLAP);
                                System.Threading.Thread.Sleep(2000);
                                BotSpeak(b, getSpeech(78));
                                if (waitForAnswer(false) == true)
                                {
                                    BotSpeak(b, getSpeech(47));
                                }
                                else
                                {
                                    BotSpeak(b, getSpeech(44));
                                    op.currentstory.paragraphs.Clear();
                                    op.currentstory._id = 0;
                                    op.currentstory._title = null;
                                    l.stories.Remove(op.currentstory);
                                }
                                op.currentstory = null;
                            }
                            else
                            {
                                BotSpeak(b, getSpeech(13));
                                DoAnimation(b.AgentId, Animations.EMBARRASSED);
                                op.listening = true;
                            }

                            op.endcheck = false;
                        }                          
                    }
                    else if (op.following)
                    {
                        op.followtime++;

                        try
                        {
                            OpenMetaverse.Vector3 pos = b.Position;
                            OpenMetaverse.Vector3 pos2 = getPosition(op.tofollow);

                            if (Vector3.Distance(pos, pos2) > 2 && Vector3.Distance(sP(b.AgentId).Velocity, Vector3.Zero) < 0.1)
                            {
                                if (getScene(b.AgentId) != getScene(op.tofollow))
                                {
                                    op.following = false;
                                    BotIM(b, op.tofollow, "Sorry, but I can't go outside my region!");
                                }
                                goTo(b, pos2);
                            }

                            //if (Vector3.Distance(pos, pos2) >= 30) { Client.Self.Teleport(Client.Network.CurrentSim.Name, pos2); }
                            //else if (Vector3.Distance(pos, pos2) >= 10) { Client.Self.Movement.AlwaysRun = true; }
                            //else { Client.Self.Movement.AlwaysRun = false; }

                            //Client.Self.Movement.Camera.LookAt(pos2, pos2);
                            //Client.Self.Movement.TurnToward(pos2);

                            if (op.hhdest != Vector3.Zero && op.askWait <= 0 && Vector3.Distance(pos, op.hhdest) > 10)
                            {
                                if (s.botType == 0)
                                {
                                    if (rnd.Next(1000) <= 30) // Ask to hear story
                                    {
                                        op.askWait = 100;
                                        if (sP(b.AgentId).Velocity == Vector3.Zero && op.waiting == false && op.currentstory == null)
                                        {
                                            BotSpeak(b, getSpeech(7), true); // "Say... wanna tell me a story?"
                                            if (waitForAnswer(false) == true)
                                            {
                                                DoAnimation(b.AgentId, Animations.CLAP);
                                                BotSpeak(b, getSpeech(40));
                                                askForStory(op.tofollow);
                                            }
                                            else
                                            {
                                                DoAnimation(b.AgentId, Animations.SHRUG);
                                                BotSpeak(b, getSpeech(44));
                                            }
                                        }
                                    }
                                    else if (rnd.Next(1000) <= 5 && l.stories.Count > 0) // Ask to tell one
                                    {
                                        op.askWait = 100;
                                        if (op.waiting == false && op.currentstory == null)
                                        {
                                            BotSpeak(b, getSpeech(60), true);
                                            if (waitForAnswer(false) == true)
                                            {
                                                DoAnimation(b.AgentId, Animations.CLAP);
                                                BotSpeak(b, getSpeech(41), false);
                                                tellStory(b, op.tofollow);
                                                System.Threading.Thread.Sleep(2000);
                                            }
                                            else
                                            {
                                                DoAnimation(b.AgentId, Animations.SHRUG);
                                                BotSpeak(b, getSpeech(44), false);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            op.following = false; op.tofollow = UUID.Zero;
                        }

                        if (Vector3.Distance(b.Position, op.hhdest) <= 10 && op.waiting == false) // At destination! (Vector3.Distance(Client.Self.SimPosition, op.hhdest) <= 10 )
                        {
                            bool stop = true;

                            if (op.storytelling)
                            {
                                if (op.storywaituntilover == false)
                                {
                                    DoAnimation(b.AgentId, Animations.JUMP_FOR_JOY);
                                    BotSpeak(b, getSpeech(61), false);
                                    System.Threading.Thread.Sleep(3000);
                                    BotSpeak(b, getSpeech(63), false);
                                    if (waitForAnswer(false) == false) { BotSpeak(b, getSpeech(42), false); }
                                    else
                                    {
                                        op.storywaituntilover = true;
                                        stop = false;
                                        BotSpeak(b, getSpeech(43), false);
                                    }
                                }
                                else { stop = false; }
                            }
                            else
                            {
                                DoAnimation(b.AgentId, Animations.JUMP_FOR_JOY);
                                BotSpeak(b, getSpeech(61), false);
                            }

                            if (stop)
                            {
                                op.following = false;
                                System.Threading.Thread.Sleep(3000);
                                DoAnimation(b.AgentId, Animations.HELLO);
                                BotSpeak(b, getSpeech(62), false);
                                System.Threading.Thread.Sleep(3000);
                                goTo(b, op.hhdest);
                                op.hhdest = Vector3.Zero;
                            }
                        }
                    }
                    else // Idle!
                    {
                        if (true) // if (ready == true)
                        {
                            bool began = false;

                            if (s.idleTime > 0 && op.noticelist.Count == 0) { s.idleTime--; if (s.idleTime == 0) { began = true; } }

                            if (s.moveType == 2 && op.noticelist.Count == 0) // Routine!
                            {
                                if (s.idleTime <= 0)
                                {
                                    if (op.currentroutine == -1 && l.routines.Count > 0)
                                    {
                                        op.currentroutine = rnd.Next(l.routines.Count);
                                    }
                                    if (op.currentroutine != -1)
                                    {
                                        if (op.routinestage == -1 && l.routines[op.currentroutine].routineLocs.Count > 0)
                                        {
                                            op.routinestage++;
                                            BotSpeak(b, l.routines[op.currentroutine].routineSpeech[op.routinestage], false);
                                            s.wanderDest = l.routines[op.currentroutine].routineLocs[op.routinestage];
                                        }
                                    }

                                    if (op.currentroutine != -1 && op.routinestage != -1)
                                    {
                                        if (began)
                                        {
                                            if (l.routines[op.currentroutine].routineSpeech[op.routinestage] != "")
                                                BotSpeak(b, l.routines[op.currentroutine].routineSpeech[op.routinestage], false);
                                        }

                                        if (s.wanderDest == Vector3.Zero && l.routines[op.currentroutine].routineLocs[op.routinestage] != Vector3.Zero)
                                            s.wanderDest = l.routines[op.currentroutine].routineLocs[op.routinestage];

                                        if (Vector3.Distance(b.Position, s.wanderDest) < 4 || (s.wanderDest == Vector3.Zero && l.routines[op.currentroutine].routineLocs[op.routinestage] == Vector3.Zero))
                                        {
                                            s.idleTime = l.routines[op.currentroutine].routinePauses[op.routinestage] * 2; // *2 because this event actually runs every half a second, not every second...
                                            s.wanderDest = Vector3.Zero;

                                            if (l.routines[op.currentroutine].routineLocs.Count > op.routinestage + 1) { op.routinestage++; }
                                            else if (l.routines[op.currentroutine].looped) { op.routinestage = 0; }
                                            else { op.currentroutine = -1; op.routinestage = -1; }
                                            //MessageBox.Show(op.routinestage.ToString()); 

                                            // Changed routine! Unless didn't.
                                            if (op.currentroutine != -1)
                                                s.wanderDest = l.routines[op.currentroutine].routineLocs[op.routinestage];
                                        }
                                        else if (s.wanderDest != Vector3.Zero) { goTo(b, s.wanderDest); }

                                        if (s.wanderDest != Vector3.Zero)
                                        {
                                            // To prevent being stuck
                                            if (Vector3.Distance(Vector3.Zero, sP(b.AgentId).Velocity) < 0.4 && Vector3.Distance(Vector3.Zero, sP(b.AgentId).Velocity) >= 0.1)
                                                op.stuckTime++;
                                            else { op.stuckTime = 0; }

                                            if (op.stuckTime >= 6)
                                            {
                                                sP(b.AgentId).Teleport(s.wanderDest);
                                                op.stuckTime = 0;
                                            }
                                        }
                                    }
                                }
                            }

                            if ((s.moveType == 0 || s.moveType == 3) && op.noticelist.Count == 0) // Wander (or Idle and Wander)
                            {
                                if (s.idleTime <= 0)
                                {
                                    Vector3 anc = b.Position;
                                    if (s.moveType == 3 && s.idleLoc != Vector3.Zero) { anc = s.idleLoc; }
                                    if (s.wanderDest == Vector3.Zero)
                                    {
                                        int att = 0;
                                        Scene sce = getScene(b.AgentId);
                                        while (true)
                                        {
                                            att++;
                                            float x = anc.X + (-10 + rnd.Next(20));
                                            float y = anc.Y + (-10 + rnd.Next(20));
                                            float h = getHeight(new Vector3(x, y, 0), sce);
                                            s.wanderDest = new Vector3(x, y, h);
                                            if ((int)h == (int)getHeight(anc, sce)) { break; }
                                            else if (att > 99) { break; }
                                        }
                                        //m_log.DebugFormat("New wanderdest ({0})", wanderDest.ToString());
                                    }

                                    if (Vector3.Distance(b.Position, s.wanderDest) < 4)
                                    {
                                        s.idleTime = 10;

                                        int att = 0;
                                        Scene sce = getScene(b.AgentId);
                                        while (true)
                                        {
                                            att++;
                                            float x = anc.X + (-10 + rnd.Next(20));
                                            float y = anc.Y + (-10 + rnd.Next(20));
                                            float h = getHeight(new Vector3(x, y, 0), sce);
                                            s.wanderDest = new Vector3(x, y, h);
                                            if ((int)h == (int)getHeight(anc, sce)) { break; }
                                            else if (att > 99) { break; }
                                        }
                                        //m_log.DebugFormat("New wanderdest ({0})", s.wanderDest.ToString());
                                    }
                                    else if (s.wanderDest != Vector3.Zero && Vector3.Distance(Vector3.Zero, sP(b.AgentId).Velocity) <= 0.1)
                                    {
                                        goTo(b, s.wanderDest);
                                    }
                                }

                                if (s.wanderDest != Vector3.Zero)
                                {
                                    // To prevent being stuck
                                    if (Vector3.Distance(Vector3.Zero, sP(b.AgentId).Velocity) < 0.4 && Vector3.Distance(Vector3.Zero, sP(b.AgentId).Velocity) >= 0.1)
                                        op.stuckTime++;
                                    else op.stuckTime = 0;

                                    if (op.stuckTime >= 6)
                                        sP(b.AgentId).Teleport(op.currentdest);
                                    op.stuckTime = 0;
                                }
                            }
                            else if (s.moveType == 1) // Idle
                            {
                                if (s.idleLoc != Vector3.Zero)
                                {
                                    if (Vector3.Distance(b.Position, s.idleLoc) > 2)
                                        goTo(b, s.idleLoc);
                                }
                            }

                            op.prevPosition = b.Position; // Keeps track of where it was last second. Useful for gauging actual speed

                            if (s.botType < 2) // If not an NPC...
                            {
                                UUID tohitch = UUID.Zero;

                                try
                                {
                                    List<UUID> ppl = new List<UUID>();
                                    List<Vector3> locas = new List<Vector3>();
                                    Vector3 pos = b.Position;
                                    getScene(b.AgentId).SceneGraph.GetCoarseLocations(out locas, out ppl, 99);
                                    for (int i = 0; i < ppl.Count; i++)
                                    {
                                        if (Vector3.Distance(pos, locas[i]) <= 15 && ppl[i] != b.AgentId)
                                        {
                                            if (Vector3.Distance(pos, op.currentdest) > 5)
                                            {
                                                sP(b.AgentId).ResetMoveToTarget();
                                                op.currentdest = pos;
                                            }
                                            s.idleTime = 10;

                                            if (hasAccess(ppl[i]) && !bot.IsNPC(ppl[i],getScene(ppl[i])))
                                            {
                                                if (sP(ppl[i]).Velocity == Vector3.Zero && Vector3.Distance(pos, locas[i]) <= 5)
                                                {
                                                    if (!op.greetlist.Contains(ppl[i])) // If they're not on the list of people we've greeted...
                                                    {
                                                        bot.StopMoveToTarget(b.AgentId, getScene(b.AgentId));
                                                        greet(b, ppl[i]);
                                                        rememberPerson(s, ppl[i], 1);
                                                        op.noticelist.Add(ppl[i]);
                                                        if (op.hhdest != Vector3.Zero) { tohitch = ppl[i]; } // If needs to hitchhike...
                                                    }
                                                }
                                                else
                                                {
                                                    if (FindClosestAvatar(b.AgentId) == ppl[i])
                                                    {
                                                        if (!op.noticelist.Contains(ppl[i]))
                                                        {
                                                            op.noticelist.Add(ppl[i]);
                                                            bot.StopMoveToTarget(b.AgentId, getScene(b.AgentId));
                                                            DoAnimation(b.AgentId, Animations.HELLO);
                                                            BotSpeak(b, getSpeech(64), false);
                                                        }
                                                        //Client.Self.Movement.TurnToward(getPosition(kvp.Key));
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (op.noticelist.Contains(ppl[i]))
                                            {
                                                op.noticelist.Remove(ppl[i]);
                                                if (!op.greetlist.Contains(ppl[i]))
                                                {
                                                    DoAnimation(b.AgentId, Animations.IMPATIENT);
                                                    BotSpeak(b, getSpeech(65), false);
                                                }
                                            }
                                            if (op.greetlist.Contains(ppl[i])) { op.greetlist.Remove(ppl[i]); }
                                        }
                                    }
                                }
                                catch { }

                                if (tohitch != UUID.Zero) // Doesn't work when run in the above foreach loop. I imagine it has to do with the delegate doing something thread-related.
                                {
                                    System.Threading.Thread.Sleep(1000);

                                    if (s.botType == 1) // Tour Guide
                                    {
                                        // If not giving a tour, ask if they'd like one. If currently giving one, then offer they follow.
                                        if (state == 0) { askToTour(b, tohitch); }
                                    }
                                    else if (s.botType == 0) // HitchBOT
                                    {
                                        //Client.Self.Movement.TurnToward(op.hhdest);
                                        //string lala = "";
                                        if (op.hhdest != Vector3.Zero)
                                        {
                                            DoAnimation(b.AgentId, Animations.POINT_YOU);
                                            try
                                            {
                                                Vector3 pos = b.Position;
                                                //lala = GetWhere(op.hhdest);
                                                float distance = 0;
                                                try { distance = (float)(Vector3.Distance(pos, op.hhdest) / 1609.34); } // Get out of here divide by zero error asklfjasgklja
                                                catch { }
                                                string direction = "";
                                                int angle = (int)(Math.Atan2(op.hhdest.Y - pos.Y, op.hhdest.X - pos.X) * 180 / Math.PI);
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
                                                BotSpeak(b, String.Format(getSpeech(66), op.hhdest, distance.ToString(), Vector3.Distance(sP(b.AgentId).OffsetPosition, op.hhdest), direction));
                                                System.Threading.Thread.Sleep(3000);
                                                //BotSpeak("I am on my way to " + op.hhcity + ", which is about " + distance.ToString() + " miles (" + Vector3.Distance(Client.Self.SimPosition, op.hhdest) + " meters) going " + direction + ". (" + op.hhdest + ")", false);
                                            }
                                            catch { BotSpeak(b, String.Format(getSpeech(67), op.hhdest)); System.Threading.Thread.Sleep(3000); } // "I am on my way to [destination]."
                                        }

                                        //Client.Self.Movement.TurnToward(A.Position);
                                        askToFollow(b, tohitch);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (time % 300 == 0)
            {
                AvatarAppearance appea = null;
                foreach (Scene scene in m_scenes)
                {
                    List<ScenePresence> foo3 = scene.GetScenePresences();
                    foreach (ScenePresence sp in foo3) { if (sp.Appearance != null) { appea = sp.Appearance; break; } }
                    if (appea != null)
                    {
                        int i = 0;
                        for (int bi = 0; bi < l.bots.Count; bi++)
                        {
                            botSettings s = l.bots[bi]; // Get that bot's settings file...

                            bool botpresent = false;
                            foreach (ScenePresence sp in foo3) { if (sp.Firstname == s.name) { botpresent = true; } }
                            if (s.homeRegion == scene.RegionInfo.RegionID && !botpresent)
                            {
                                m_log.Debug("[MockingBOT]: Adding bot.");
                                try
                                {
                                    UUID foo = bot.CreateNPC(s.name, "McBOT", s.homeLoc, UUID.Parse("d55f3e64-0290-4a5d-9292-d7db6f96bd7c"), false, scene, appea); // Aw geez how do I get default appearance again goddang

                                    NPCAvatar foo2 = (NPCAvatar)(bot.GetNPC(foo, getScene(foo)));
                                    bots.Add(foo);                                    
                                    npcavatars.Add(foo2);
                                    npcid.Add(bi);
                                    npcop.Add(new botOperator(foo2.Name));

                                    m_log.DebugFormat("[MockingBOT]: Successfully added bot ({0},{1},{2},{3})",bots.Count,npcavatars.Count,npcid.Count,npcop.Count);
                                }
                                catch (Exception ex) { m_log.Debug("[MockingBOT]: Bot failed to load (" + ex.Message + ")"); }
                            }
                            i++;
                        }
                    }
                }
                /*
                for (int bi = 0; bi < npcid.Count; bi++)
                {
                    s = l.bots[npcid[bi]];
                    NPCAvatar b = npcavatars[bi];
                    op = npcop[bi];

                    if (s.botType == 0)
                    {
                        if (op.following == true && b.Position != Vector3.Zero)
                        {
                            try
                            {
                                string here = GetWhere(b.Position);
                                if (op.currentcity != here)
                                {
                                    op.currentcity = here;
                                    string st = String.Format(getSpeech(68), here);
                                    BotSpeak(b, st);
                                    BotTrivia(b, here);
                                }
                                op.currentcity = here;
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                */
            }
        }

        // Main chat event
        private void OnChatToClients(UUID senderID, HashSet<UUID> receiverIDs, string message, ChatTypeEnum type, Vector3 fromPos, string fromName, ChatSourceType src, ChatAudibleLevel level)
        {
            if (type == ChatTypeEnum.Say)
            {
                Vector3 pos2 = fromPos;
                Scene sce = getScene(senderID);

                if (!bot.IsNPC(senderID, sce))
                {
                    string say = "";
                    List<string> res = getCompoundSentences(Simplify(message.ToLower()));

                    foreach (string msg in res)
                    {
                        if (message == "!createbot")
                        {
                            string nam = "Milly";
                            int rand = rnd.Next(7); if (rand == 1) { nam = "Dorcas"; } else if (rand == 2) { nam = "Ruth"; } else if (rand == 3) { nam = "Martha"; } else if (rand == 4) { nam = "Liza"; } else if (rand == 5) { nam = "Sarah"; } else if (rand == 6) { nam = "Alice"; }
                            UUID foo = bot.CreateNPC(nam, "McBOT", pos2, senderID, false, sce, getAppearance(senderID));
                            bots.Add(foo);
                            NPCAvatar foo2 = (NPCAvatar)(bot.GetNPC(foo, getScene(foo)));
                            npcavatars.Add(foo2);
                            npcid.Add(l.bots.Count);
                            int newid = l.bots.Count;

                            botSettings newbot = new botSettings();
                            l.bots.Add(newbot);
                            foreach (textNode t in l.nodes) { newbot.allowednodes.Add(t._id); } // By default, allow all.
                            foreach (botStories s in l.stories) { newbot.allowedstories.Add(s._id); } // By default, allow all.
                            foreach (botRoutines r in l.routines) { newbot.allowedroutines.Add(r._id); } // By default, allow all.
                            foreach (botTours t in l.tours) { newbot.allowedtours.Add(t._id); } // By default, allow all.

                            l.bots[newid].name = nam;
                            l.bots[newid].description = "This bot was created in-world by " + fromName + ".";

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


                            return;
                        }
                        else if (msg.Contains("!tweet"))
                        {
                            string foo;
                            foo = msg.Remove(0, 6);
                            foo = foo.Trim();
                            string tw = fromName + " says: " + foo;
                            if (tw.Length < 140)
                            {
                                Tweet(tw);
                                m_log.Debug("[MockingBOT]: Tweeted.");
                            }
                            else { m_log.Debug("[MockingBOT]: " + String.Format(getSpeech(76), tw.Length, (tw.Length - 140))); } //BotSpeak("That's too long to tweet! It was " + tw.Length + " characters - " + (tw.Length-140) + " characters too many!"); }
                        }
                        else if (msg.Contains("!save"))
                        {
                            SaveSettings(Environment.CurrentDirectory + "\\botsettings.xml");
                        }
                        else if (msg.Contains("!load"))
                        {
                            LoadSettings(Environment.CurrentDirectory + "\\botsettings.xml");
                        }

                        List<NPCAvatar> listeners = getListeners(senderID);
                        List<List<string>> spst = new List<List<string>>();
                        foreach (NPCAvatar a in listeners) { spst.Add(new List<string>()); }

                        for (int i = 0; i < listeners.Count; i++)
                        {
                            int foo2 = npcavatars.IndexOf(listeners[i]);
                            NPCAvatar b = listeners[i];
                            botSettings s = l.bots[npcid[foo2]];
                            op = npcop[foo2];

                            Vector3 pos = b.Position;

                            if (msg.StartsWith("!"))
                            {
                                if (msg == "!sayname")
                                {
                                    //spst[i].Add("My name is " + getName(b) + "!");
                                    //NPCAvatar woo = (NPCAvatar)bot.GetNPC(b, getScene(b));
                                    BotSpeak(b, "My name is " + b.FirstName + "!");
                                }
                                else if (msg == "!sayposition")
                                {
                                    spst[i].Add("You are " + Vector3.Distance(b.Position, pos2) + " away from me.");
                                }
                                else if (msg == "!come")
                                {
                                    goTo(b.AgentId, pos2);
                                }
                                else if (msg == "!follow")
                                {
                                    if (op.following == false)
                                    {
                                        op.following = true; op.tofollow = senderID;
                                        BotSpeak(b, getSpeech(2));
                                    }
                                    else
                                    {
                                        BotSpeak(b, getSpeech(4));
                                    }
                                }
                                else if (msg == "!stop")
                                {
                                    if (op.following == true)
                                    {
                                        op.following = false; op.tofollow = UUID.Zero;
                                        BotSpeak(b, getSpeech(5));
                                    }
                                    else
                                    {
                                        BotSpeak(b, getSpeech(6));
                                    }
                                }
                                else if (msg == "!teleport")
                                {
                                    //Client.Self.SendTeleportLure(senderID);
                                    //spst[i].Add(getSpeech(30));
                                }
                                else if (msg == "!where")
                                {
                                    //BotSpeak("I am at " + Client.Network.CurrentSim.Name + ". My position is " + pos.X + ", " + pos.Y);
                                }
                                else if (msg == "!point")
                                {
                                    DoAnimation(b.AgentId, Animations.POINT_YOU);
                                    //Client.Self.AnimationStart(Animations.POINT_YOU, true);
                                }
                                else if (msg == "!whome")
                                {
                                    spst[i].Add("You are " + fromName + ", at position " + fromPos);
                                }
                                else if (msg == "!look")
                                {
                                    //Client.Self.Movement.Camera.LookDirection(pos2);
                                }
                                else if (msg == "!whoclosest")
                                {
                                    UUID closest = FindClosestAvatar(b.AgentId);
                                    string balls = closest.ToString(); // Maturity.
                                    spst[i].Add("That would be " + balls);
                                }
                                else if (msg == "!tour")
                                {
                                    if (l.tours.Count > 0)
                                    {
                                        if (op.currenttour == null)
                                        {
                                            int tourgrab = rnd.Next(l.tours.Count);
                                            op.currenttour = l.tours[tourgrab];
                                        }
                                        op.tourist = senderID;
                                        goToTour(b);
                                    }
                                    else
                                    {
                                        spst[i].Add(getSpeech(72));
                                    }
                                }
                                else if (msg == "!setidle")
                                {
                                    s.idleLoc = pos2;
                                }
                                else if (msg == "!saveappearance")
                                {
                                    //s.appe = getAppearance(senderID);
                                    m_log.Debug(getAppearance(senderID).ToString());
                                }
                                else if (msg == "!learntour")
                                {
                                    op.currenttour = new botTours();
                                    l.tours.Add(op.currenttour);
                                    op.currenttour._id = l.tours.Count;
                                    op.currenttour.enabled = false;

                                    op.learningtour = true;
                                    spst[i].Add(getSpeech(31));
                                }
                                else if (msg == "!tourhere")
                                {
                                    if (op.learningtour)
                                    {
                                        op.tourStage++;
                                        List<string> woo = new List<string>();
                                        op.currenttour.tourInfo.Add(woo);
                                        op.currenttour.tourLocs.Add(pos2);
                                        spst[i].Add(String.Format(getSpeech(32), pos2.X + ", " + pos2.Y + ", " + pos2.Z));
                                    }
                                }
                                else if (msg == "!finishtour")
                                {
                                    if (op.learningtour == true)
                                    {
                                        op.learningtour = false;
                                        op.currenttour = null;
                                        spst[i].Add(getSpeech(36));
                                    }
                                    else
                                    {
                                        spst[i].Add(getSpeech(37));
                                    }
                                }
                                else if (msg == "!stoptour")
                                {
                                    if (op.touring == true)
                                    {
                                        op.touring = false;
                                        op.currenttour = null;
                                        spst[i].Add(getSpeech(27));
                                    }
                                    else
                                    {
                                        spst[i].Add(getSpeech(28));
                                    }
                                }
                                else if (msg == "!learnroutine")
                                {
                                    botRoutines foo = new botRoutines();
                                    l.routines.Add(foo);
                                    foo._id = l.routines.Count;
                                    op.currentroutine = foo._id - 1;

                                    l.routines[op.currentroutine].routineLocs.Add(Vector3.Zero);
                                    l.routines[op.currentroutine].routinePauses.Add(0);
                                    l.routines[op.currentroutine].routineSpeech.Add("");
                                    l.routines[op.currentroutine].routineAnimations.Add(UUID.Zero);
                                    op.routinestage = 0;

                                    op.learningroutine = true;
                                    spst[i].Add(getSpeech(31));
                                }
                                else if (msg == "!routehere")
                                {
                                    if (op.learningroutine)
                                    {
                                        l.routines[op.currentroutine].routineLocs[op.routinestage] = pos2;
                                        spst[i].Add(String.Format(getSpeech(32), pos2.X + ", " + pos2.Y + ", " + pos2.Z));
                                    }
                                }
                                else if (msg == "!routenextstage")
                                {
                                    if (op.learningroutine)
                                    {
                                        l.routines[op.currentroutine].routineLocs.Add(pos2);
                                        l.routines[op.currentroutine].routinePauses.Add(0);
                                        l.routines[op.currentroutine].routineSpeech.Add("");
                                        l.routines[op.currentroutine].routineAnimations.Add(UUID.Zero);
                                        op.routinestage++;
                                        spst[i].Add("Now taking notes for stage " + op.routinestage.ToString());
                                    }
                                }
                                else if (msg == "!routinecheck")
                                {
                                    if (op.learningroutine)
                                    {
                                        spst[i].Add("Now taking notes for stage " + op.routinestage.ToString() + " in routine " + op.currentroutine.ToString() + ".");
                                    }
                                    else
                                    {
                                        if (s.idleTime > 0) { spst[i].Add("Now op.waiting " + Math.Ceiling(s.idleTime * 0.5).ToString() + " seconds; will continue performing stage " + op.routinestage.ToString() + " in routine " + op.currentroutine.ToString() + "."); }
                                        else { spst[i].Add("Now performing stage " + op.routinestage.ToString() + " in routine " + op.currentroutine.ToString() + "."); }
                                    }
                                }
                                else if (msg == "!finishroutine")
                                {
                                    if (op.learningroutine)
                                    {
                                        op.learningroutine = false;
                                        op.currentroutine = -1;
                                        op.routinestage = -1;
                                        spst[i].Add(getSpeech(36));
                                    }
                                }
                                else if (msg == "!proceed")
                                {
                                    if (op.touring == true && op.touristPresent == true)
                                    {
                                        op.tourStage++;
                                        goToTour(b);
                                    }
                                }
                                else if (msg == "!hearstory")
                                {
                                    spst[i].Add("I'm listenin'!");
                                    askForStory(senderID);
                                }
                                else if (msg == "!tellstory")
                                {
                                    if (l.stories.Count > 0)
                                    {
                                        if (op.currentstory == null)
                                        {
                                            int storygrab = rnd.Next(l.stories.Count);
                                            op.currentstory = l.stories[storygrab];
                                        }
                                        op.storytelling = true;
                                        op.listener = senderID;
                                    }
                                    else
                                    {
                                        BotSpeak(b, getSpeech(15));
                                    }
                                }
                                else if (msg.StartsWith("!changebottype"))
                                {
                                    if (msg.ToLower().Contains("hitchbot")) { s.botType = 0; }
                                    else if (msg.ToLower().Contains("tour guide")) { s.botType = 1; }
                                    else if (msg.ToLower().Contains("npc")) { s.botType = 2; }
                                }
                                else if (msg.StartsWith("!changemovetype"))
                                {
                                    if (msg.ToLower().Contains("wander")) { s.moveType = 0; }
                                    else if (msg.ToLower().Contains("idle")) { s.moveType = 1; }
                                    else if (msg.ToLower().Contains("routine")) { s.moveType = 2; }
                                }
                                else if (msg == "!weather")
                                {
                                    List<string> lala = new List<string>();
                                    getWeather(GetWoeid("Hamilton, CA"), out lala);
                                    spst[i].Add(String.Format(getSpeech(77), lala[0], lala[1], lala[2], lala[3])); //BotSpeak("The date is " + lala[0] + ". Conditions are " + lala[1] + ", with a low of " + lala[2] + " and a high of " + lala[3]);
                                }
                                else if (msg == "!wherecanada")
                                {
                                    float myx, myy;
                                    myx = (float)81.4217 - (pos.X / (float)76.5098);
                                    myy = (float)44.0436 + (pos.Y / (float)76.5098);

                                    try
                                    {
                                        //string woo = GetWhere(myy.ToString(), "-" + myx.ToString());
                                        //spst[i].Add("I am in " + woo);
                                    }
                                    catch
                                    {
                                        spst[i].Add("Invalid coordinates (" + myy.ToString() + ", -" + myx.ToString() + " )");
                                    }
                                }
                                else { spst[i].Add(getSpeech(80)); }
                            }
                            else
                            {
                                if (op.learningtour)
                                {
                                    op.currenttour.tourInfo[op.tourStage - 1].Add(msg);
                                    spst[i].Add(getSpeech(33));
                                }
                                else if (op.learningroutine)
                                {
                                    l.routines[op.currentroutine].routineSpeech[op.routinestage] = msg;
                                    spst[i].Add(getSpeech(33));
                                }
                                else if (op.listening)
                                {
                                    if (msg.Contains("the end"))
                                    {
                                        op.endcheck = true;
                                    }
                                    else if (!op.endcheck) 
                                    {
                                        op.listeningtime = 0;
                                        op.currentstory.paragraphs.Add(message); // Add to the story
                                        spst[i].Add(getSpeech(8)); // "/me nods."
                                        DoAnimation(b.AgentId, Animations.YES); // Literally makes her nod.
                                        break; // Since this is a compound sentence, this just makes it so it only adds the entire message, and then breaks it off.
                                    }
                                }
                                else if (isQuestion(msg)) // If it's a question...
                                {
                                    // Default response

                                    say = getSpeech(39);
                                    List<string> saythese = new List<string>();

                                    int usedchars = 0;
                                    List<string> foo = getKeywords(Simplify(msg));
                                    string bar = ""; foreach (string str in foo) { bar += str + " "; }
                                    m_log.Debug(bar);
                                    textNode ournode = SearchNode(s, senderID, foo);

                                    if (ournode != null)
                                    {
                                        say = ReadNode(ournode._data);
                                        saythese.Add(say);

                                        //rememberTaught(senderID, ournode._id - 1);
                                        usedchars += ournode._data.Length;

                                        op.used.Clear();
                                        textNode next = null;
                                        bool finished = false;
                                        op.used.Add(ournode);

                                        while (finished == false)
                                        {
                                            finished = true;
                                            int bluh = rnd.Next(2);
                                            List<textNode> q = new List<textNode>();

                                            if (ournode.dependencies.Count > 0) // If dependent on other pieces of information...
                                            {
                                                q = GetDependentNode(s, senderID, ournode);
                                            }
                                            //else
                                            //{ q = GetLinkedNode(ournode); } // Else, go ahead and tell us anything that might be related.

                                            if (q.Count <= 0) { q = GetLinkedNode(s, ournode); }

                                            if (q.Count > 0)
                                            {
                                                //int sel = rnd.Next(q.Count);
                                                foreach (textNode p in q)
                                                {
                                                    if (p._data.Length < (op.limit - usedchars)) // To prevent hitting the cap.
                                                    {
                                                        //say += " " + ReadNode(p._data); // I guess this is where we'll check for methods...
                                                        saythese.Add(ReadNode(p._data));
                                                        //rememberTaught(e.OwnerID, p._id - 1);
                                                        usedchars += p._data.Length;
                                                        op.used.Add(p);
                                                        next = p;
                                                        finished = false;
                                                    }
                                                    ournode = next;
                                                    //break;
                                                }
                                            }
                                        }
                                    }
                                    //else { saythese.Add(say); }
                                    //if (say.Length > op.limit) { say.Remove(op.limit); }

                                    foreach (string aaaa in saythese)
                                    {
                                        spst[i].Add(aaaa);
                                    }
                                }
                                else if (msg.Contains("yes") || msg.Contains("yeah") || msg.Contains("yep") || msg.Contains("yup") || msg.Contains("mhm") ||
                                    msg.Contains("affirmative") || msg.Contains("uh huh") || msg.Contains("yis") || msg.Contains("kay") || msg.Contains("ok") ||
                                    msg.Contains("all right") || msg.Contains("sure") || msg.Contains("mmk") || msg.Contains("aye") || msg.Contains("aii"))
                                { op.yesno = 1; }
                                else if (msg.Contains("no") || msg.Contains("nah") || msg.Contains("naw") || msg.Contains("not") || msg.Contains("nay") ||
                                    msg.Contains("don't") || msg.Contains("negative") || msg.Contains("negatory"))
                                { op.yesno = 0; }
                                else { handleDialogue(b, senderID, msg); }
                            }
                        }

                        for (int i = 0; i < listeners.Count; i++)
                        {
                            int foo2 = npcavatars.IndexOf(listeners[i]);
                            NPCAvatar b = listeners[i];
                            s = l.bots[foo2];
                            op = npcop[foo2];

                            if (op.name != b.Name) { m_log.Debug("Shit happened (3)."); }

                            m_log.Debug(b.FirstName + " has " + spst[i].Count.ToString() + " strings.");

                            if (spst[i].Count > 0)
                            {
                                if (msg.Contains(listeners[i].FirstName))
                                {
                                    foreach (string spstr in spst[i])
                                    {
                                        BotSpeak(b, spstr);
                                    }
                                }
                                else
                                {
                                    bool checkothers = false; string othername = "";
                                    for (int ii = 0; ii < listeners.Count; ii++)
                                    {
                                        if (spst[ii].Count > 0 && msg.Contains(listeners[ii].FirstName))
                                        {
                                            checkothers = true; othername = listeners[ii].FirstName; break;
                                        }
                                        else if (msg.Contains(listeners[ii].FirstName)) { BotSpeak(listeners[ii], getSpeech(39) + " " + listeners[i].FirstName + "?"); }
                                    }
                                    if (!checkothers)
                                    {
                                        foreach (string spstr in spst[i])
                                        {
                                            BotSpeak(b, spstr);
                                        }
                                        break;
                                    }
                                }
                            }
                            else if (msg.Contains(listeners[i].FirstName))
                            {
                                bool checkothers = false; string othername = "";
                                for (int ii = 0; ii < listeners.Count; ii++)
                                {
                                    if (ii != i && spst[ii].Count > 0) { checkothers = true; othername = listeners[ii].FirstName; break; }
                                }
                                if (checkothers) { BotSpeak(b, getSpeech(39) + " " + othername + "?"); } else { BotSpeak(b, getSpeech(39)); break; }
                            }
                            else if (i == listeners.Count - 1 && isQuestion(msg))
                            {
                                BotSpeak(b, getSpeech(39));
                            }
                        }
                    }
                }
                else
                {
                    m_log.DebugFormat("[MockingBOT]: " + getName(senderID) + " said: " + message);
                }
            }
        }
        // Main IM event
        private void OnInstantMessage(OpenSim.Framework.IClientAPI remoteclient, OpenSim.Framework.GridInstantMessage im)
        {
            if (im.dialog == 0) // Basic dialogue -- like 'Say'.
            {
                string say = "";

                // (I can't believe GUID and UUID are the exact same but I still have to pull off these freakin' gymnastics to get one from the other. UUID lets you get GUID easy though.)
                UUID uu = UUID.Parse(im.toAgentID.ToString());
                UUID uu2 = UUID.Parse(im.fromAgentID.ToString());

                int foo2 = bots.IndexOf(uu);
                NPCAvatar b = npcavatars[foo2];
                botSettings s = l.bots[npcid[foo2]];
                botOperator op = npcop[foo2];

                Vector3 pos2 = im.Position;
                Scene sce = getScene(uu2);

                UUID sess = UUID.Parse(im.imSessionID.ToString());

                List<string> spst = new List<string>();

                if (bot.IsNPC(uu, getScene(uu)))
                {
                    List<string> res = getCompoundSentences(Simplify(im.message.ToLower()));

                    foreach (string msg in res)
                    {
                        if (msg == "!deletebot")
                        {
                            l.bots.Remove(s);
                        }
                        else if (msg.Contains("!tweet"))
                        {
                            string foo;
                            foo = msg.Remove(0, 6);
                            foo = foo.Trim();
                            string tw = im.fromAgentName + " says: " + foo;
                            if (tw.Length < 140)
                            {
                                Tweet(tw);
                                m_log.Debug("[MockingBOT]: Tweeted.");
                            }
                            else { m_log.Debug("[MockingBOT]: " + String.Format(getSpeech(76), tw.Length, (tw.Length - 140))); } //BotSpeak("That's too long to tweet! It was " + tw.Length + " characters - " + (tw.Length-140) + " characters too many!"); }
                        }
                        else
                        {

                            Vector3 pos = b.Position;

                            if (msg.StartsWith("!"))
                            {
                                if (msg == "!sayname")
                                {
                                    //spst.Add("My name is " + getName(b) + "!");
                                    //NPCAvatar woo = (NPCAvatar)bot.GetNPC(b, getScene(b));
                                    spst.Add("My name is " + b.FirstName + "!");
                                }
                                else if (msg == "!sayposition")
                                {
                                    spst.Add("You are " + Vector3.Distance(b.Position, pos2) + " away from me.");
                                }
                                else if (msg == "!come")
                                {
                                    goTo(b.AgentId, pos2);
                                }
                                else if (msg == "!follow")
                                {
                                    if (op.following == false)
                                    {
                                        op.following = true; op.tofollow = uu2;
                                        BotIM(b, uu2, getSpeech(2), sess);
                                    }
                                    else
                                    {
                                        BotIM(b, uu2, getSpeech(4), sess);
                                    }
                                }
                                else if (msg == "!stop")
                                {
                                    if (op.following == true)
                                    {
                                        op.following = false; op.tofollow = UUID.Zero;
                                        BotIM(b, uu2, getSpeech(5), sess);
                                    }
                                    else
                                    {
                                        BotIM(b, uu2, getSpeech(6), sess);
                                    }
                                }
                                else if (msg == "!teleport")
                                {
                                    //Client.Self.SendTeleportLure(senderID);
                                    //spst.Add(getSpeech(30));
                                }
                                else if (msg == "!where")
                                {
                                    //BotSpeak("I am at " + Client.Network.CurrentSim.Name + ". My position is " + pos.X + ", " + pos.Y);
                                }
                                else if (msg == "!point")
                                {
                                    DoAnimation(b.AgentId, Animations.POINT_YOU);
                                    //Client.Self.AnimationStart(Animations.POINT_YOU, true);
                                }
                                else if (msg == "!whome")
                                {
                                    spst.Add("You are " + im.fromAgentName + ", at position " + im.Position);
                                }
                                else if (msg == "!look")
                                {
                                    //Client.Self.Movement.Camera.LookDirection(pos2);
                                }
                                else if (msg == "!whoclosest")
                                {
                                    UUID closest = FindClosestAvatar(b.AgentId);
                                    string balls = closest.ToString(); // Maturity.
                                    spst.Add("That would be " + balls);
                                }
                                else if (msg == "!tour")
                                {
                                    if (l.tours.Count > 0)
                                    {
                                        if (op.currenttour == null)
                                        {
                                            int tourgrab = rnd.Next(l.tours.Count);
                                            op.currenttour = l.tours[tourgrab];
                                        }
                                        op.tourist = uu2;
                                        goToTour(b);
                                    }
                                    else
                                    {
                                        spst.Add(getSpeech(72));
                                    }
                                }
                                else if (msg == "!setidle")
                                {
                                    s.idleLoc = pos2;
                                }
                                else if (msg == "!saveappearance")
                                {
                                    //s.appe = getAppearance(senderID);
                                    m_log.Debug(getAppearance(uu2).ToString());
                                }
                                else if (msg == "!learntour")
                                {
                                    op.currenttour = new botTours();
                                    l.tours.Add(op.currenttour);
                                    op.currenttour._id = l.tours.Count;
                                    op.currenttour.enabled = false;

                                    op.learningtour = true;
                                    spst.Add(getSpeech(31));
                                }
                                else if (msg == "!tourhere")
                                {
                                    if (op.learningtour)
                                    {
                                        op.tourStage++;
                                        List<string> woo = new List<string>();
                                        op.currenttour.tourInfo.Add(woo);
                                        op.currenttour.tourLocs.Add(pos2);
                                        spst.Add(String.Format(getSpeech(32), pos2.X + ", " + pos2.Y + ", " + pos2.Z));
                                    }
                                }
                                else if (msg == "!finishtour")
                                {
                                    if (op.learningtour == true)
                                    {
                                        op.learningtour = false;
                                        op.currenttour = null;
                                        spst.Add(getSpeech(36));
                                    }
                                    else
                                    {
                                        spst.Add(getSpeech(37));
                                    }
                                }
                                else if (msg == "!stoptour")
                                {
                                    if (op.touring == true)
                                    {
                                        op.touring = false;
                                        op.currenttour = null;
                                        spst.Add(getSpeech(27));
                                    }
                                    else
                                    {
                                        spst.Add(getSpeech(28));
                                    }
                                }
                                else if (msg == "!learnroutine")
                                {
                                    botRoutines foo = new botRoutines();
                                    l.routines.Add(foo);
                                    foo._id = l.routines.Count;
                                    op.currentroutine = foo._id - 1;

                                    l.routines[op.currentroutine].routineLocs.Add(Vector3.Zero);
                                    l.routines[op.currentroutine].routinePauses.Add(0);
                                    l.routines[op.currentroutine].routineSpeech.Add("");
                                    l.routines[op.currentroutine].routineAnimations.Add(UUID.Zero);
                                    op.routinestage = 0;

                                    op.learningroutine = true;
                                    spst.Add(getSpeech(31));
                                }
                                else if (msg == "!routehere")
                                {
                                    if (op.learningroutine)
                                    {
                                        l.routines[op.currentroutine].routineLocs[op.routinestage] = pos2;
                                        spst.Add(String.Format(getSpeech(32), pos2.X + ", " + pos2.Y + ", " + pos2.Z));
                                    }
                                }
                                else if (msg == "!routenextstage")
                                {
                                    if (op.learningroutine)
                                    {
                                        l.routines[op.currentroutine].routineLocs.Add(pos2);
                                        l.routines[op.currentroutine].routinePauses.Add(0);
                                        l.routines[op.currentroutine].routineSpeech.Add("");
                                        l.routines[op.currentroutine].routineAnimations.Add(UUID.Zero);
                                        op.routinestage++;
                                        spst.Add("Now taking notes for stage " + op.routinestage.ToString());
                                    }
                                }
                                else if (msg == "!routinecheck")
                                {
                                    if (op.learningroutine)
                                    {
                                        spst.Add("Now taking notes for stage " + op.routinestage.ToString() + " in routine " + op.currentroutine.ToString() + ".");
                                    }
                                    else
                                    {
                                        if (s.idleTime > 0) { spst.Add("Now op.waiting " + Math.Ceiling(s.idleTime * 0.5).ToString() + " seconds; will continue performing stage " + op.routinestage.ToString() + " in routine " + op.currentroutine.ToString() + "."); }
                                        else { spst.Add("Now performing stage " + op.routinestage.ToString() + " in routine " + op.currentroutine.ToString() + "."); }
                                    }
                                }
                                else if (msg == "!finishroutine")
                                {
                                    if (op.learningroutine)
                                    {
                                        op.learningroutine = false;
                                        op.currentroutine = -1;
                                        op.routinestage = -1;
                                        spst.Add(getSpeech(36));
                                    }
                                }
                                else if (msg == "!proceed")
                                {
                                    if (op.touring == true && op.touristPresent == true)
                                    {
                                        op.tourStage++;
                                        goToTour(b);
                                    }
                                }
                                else if (msg == "!hearstory")
                                {
                                    spst.Add("I'm listenin'!");
                                    askForStory(uu2);
                                }
                                else if (msg == "!tellstory")
                                {
                                    if (l.stories.Count > 0)
                                    {
                                        if (op.currentstory == null)
                                        {
                                            int storygrab = rnd.Next(l.stories.Count);
                                            op.currentstory = l.stories[storygrab];
                                        }
                                        op.storytelling = true;
                                        op.listener = uu2;
                                    }
                                    else
                                    {
                                        spst.Add(getSpeech(15));
                                    }
                                }
                                else if (msg.StartsWith("!changebottype"))
                                {
                                    if (msg.ToLower().Contains("hitchbot")) { s.botType = 0; }
                                    else if (msg.ToLower().Contains("tour guide")) { s.botType = 1; }
                                    else if (msg.ToLower().Contains("npc")) { s.botType = 2; }
                                }
                                else if (msg.StartsWith("!changemovetype"))
                                {
                                    if (msg.ToLower().Contains("wander")) { s.moveType = 0; }
                                    else if (msg.ToLower().Contains("idle")) { s.moveType = 1; }
                                    else if (msg.ToLower().Contains("routine")) { s.moveType = 2; }
                                }
                                else if (msg == "!weather")
                                {
                                    List<string> lala = new List<string>();
                                    //getWeather(GetWoeid("Hamilton, CA"), out lala);
                                    //spst.Add(String.Format(getSpeech(77), lala[0], lala[1], lala[2], lala[3])); //BotSpeak("The date is " + lala[0] + ". Conditions are " + lala[1] + ", with a low of " + lala[2] + " and a high of " + lala[3]);
                                }
                                else if (msg == "!wherecanada")
                                {
                                    float myx, myy;
                                    myx = (float)81.4217 - (pos.X / (float)76.5098);
                                    myy = (float)44.0436 + (pos.Y / (float)76.5098);

                                    try
                                    {
                                        //string woo = GetWhere(myy.ToString(), "-" + myx.ToString());
                                        //spst.Add("I am in " + woo);
                                    }
                                    catch
                                    {
                                        spst.Add("Invalid coordinates (" + myy.ToString() + ", -" + myx.ToString() + " )");
                                    }
                                }
                                else { spst.Add(getSpeech(80)); }
                            }
                            else
                            {
                                if (op.learningtour)
                                {
                                    op.currenttour.tourInfo[op.tourStage - 1].Add(msg);
                                    spst.Add(getSpeech(33));
                                }
                                else if (op.learningroutine)
                                {
                                    l.routines[op.currentroutine].routineSpeech[op.routinestage] = msg;
                                    spst.Add(getSpeech(33));
                                }
                                else if (op.listening)
                                {
                                    if (msg.Contains("the end"))
                                    {
                                        op.endcheck = true; // Triggers the bot asking if it's over (which involves a pause and thus can only be done in the 'OnTick2' method without causing problems).
                                    }
                                    else
                                    {
                                        op.listeningtime = 0;
                                        op.currentstory.paragraphs.Add(im.message); // Add to the story
                                        spst.Add(getSpeech(8)); // "/me nods."
                                        DoAnimation(b.AgentId, Animations.YES); // Literally makes her nod.
                                        break; // Since this is a compound sentence, this just makes it so it only adds the entire message, and then breaks it off.
                                    }
                                }
                                else if (isQuestion(msg)) // If it's a question...
                                {
                                    say = "";
                                    List<string> saythese = new List<string>();

                                    int usedchars = 0;
                                    List<string> foo = getKeywords(Simplify(msg));
                                    string bar = ""; foreach (string str in foo) { bar += str + " "; }
                                    m_log.Debug(bar);
                                    textNode ournode = SearchNode(s, uu2, foo);

                                    if (ournode != null)
                                    {
                                        say = ReadNode(ournode._data);
                                        saythese.Add(say);

                                        //rememberTaught(senderID, ournode._id - 1);
                                        usedchars += ournode._data.Length;

                                        op.used.Clear();
                                        textNode next = null;
                                        bool finished = false;
                                        op.used.Add(ournode);

                                        while (finished == false)
                                        {
                                            finished = true;
                                            int bluh = rnd.Next(2);
                                            List<textNode> q = new List<textNode>();

                                            if (ournode.dependencies.Count > 0) // If dependent on other pieces of information...
                                            {
                                                q = GetDependentNode(s, uu2, ournode);
                                            }
                                            //else
                                            //{ q = GetLinkedNode(ournode); } // Else, go ahead and tell us anything that might be related.

                                            if (q.Count <= 0) { q = GetLinkedNode(s, ournode); }

                                            if (q.Count > 0)
                                            {
                                                //int sel = rnd.Next(q.Count);
                                                foreach (textNode p in q)
                                                {
                                                    if (p._data.Length < (op.limit - usedchars)) // To prevent hitting the cap.
                                                    {
                                                        //say += " " + ReadNode(p._data); // I guess this is where we'll check for methods...
                                                        saythese.Add(ReadNode(p._data));
                                                        //rememberTaught(e.OwnerID, p._id - 1);
                                                        usedchars += p._data.Length;
                                                        op.used.Add(p);
                                                        next = p;
                                                        finished = false;
                                                    }
                                                    ournode = next;
                                                    //break;
                                                }
                                            }
                                        }
                                    }

                                    foreach (string aaaa in saythese)
                                    {
                                        spst.Add(aaaa);
                                    }
                                }
                                else if (msg.Contains("yes") || msg.Contains("yeah") || msg.Contains("yep") || msg.Contains("yup") || msg.Contains("mhm") ||
                                    msg.Contains("affirmative") || msg.Contains("uh huh") || msg.Contains("yis") || msg.Contains("kay") || msg.Contains("ok") ||
                                    msg.Contains("all right") || msg.Contains("sure") || msg.Contains("mmk") || msg.Contains("aye") || msg.Contains("aii"))
                                { op.yesno = 1; }
                                else if (msg.Contains("no") || msg.Contains("nah") || msg.Contains("naw") || msg.Contains("not") || msg.Contains("nay") ||
                                    msg.Contains("don't") || msg.Contains("negative") || msg.Contains("negatory"))
                                { op.yesno = 0; }
                                else { handleDialogue(b, uu2, msg, sess); }
                            }
                        }

                        m_log.Debug(b.FirstName + " has " + spst.Count.ToString() + " strings.");

                        if (spst.Count > 0)
                        {
                            foreach (string spstr in spst)
                            {
                                BotIM(b, uu2, spstr, sess);
                            }
                        }
                        else if (isQuestion(msg))
                        {
                            BotSpeak(b, getSpeech(39));
                        }
                    }
                }
            }
        }
        
        // The following are meant for MockingBOT's dialogue node system. The 'SearchNode' just searches for the most relevant node. GetLinked/GetDependent is used to find any nodes 'attached' to another. 
        // 'ReadNode' is defunct; at one point, you could call functions within a node, but this turned out to be too problematic at the time.
        static textNode SearchNode(botSettings b, UUID a, List<string> chec)
        {
            Dictionary<textNode, int> searchquery = new Dictionary<textNode, int>(); // For organizing nodes by priority.
            string keywordsused = "";

            foreach (textNode t in l.nodes) // For every node...
            {
                if ((t._state != state && t._state != -1) || !b.allowednodes.Contains(t._id)) { continue; } // If not allowed to this bot, ignore this one.

                int pri = 0; // Set default priority (0)
                if (!string.IsNullOrEmpty(t._subject))
                {
                    string str = t._subject.ToLower();
                    string[] strs = str.Split(' ');
                    foreach (string word in chec) // For every word in our search string...
                    {
                        for (int i = 0; i < strs.Length; i++) // For every keyword in this node...
                        {
                            if (strs[i] == word.ToLower()) // If the keyword matches the word we're looking for...
                            {
                                pri += 10; // Priority+
                                keywordsused += word + " "; // Add to keywords we've found here.
                            }
                            else
                            {
                                if (strs[i].Contains("-")) // Hyphenated word.
                                {
                                    // Basically, check if all hyphenated words are in our search string.
                                    List<string> andchec = strs[i].Split('-').ToList();
                                    int req = andchec.Count;
                                    string wordsused = "";
                                    foreach (string w in chec) { if (andchec.Contains(w)) { req -= 1; wordsused += "" + w + " "; andchec.Remove(w); } }
                                    if (req <= 0) { pri += 10; keywordsused += wordsused; }
                                }
                                if (strs[i].Contains("||")) // Unused 'or' check.
                                {

                                }
                            }
                        }
                    }
                    if (pri > 0) { searchquery.Add(t, pri); } // If we changed its priority at all, add to our list.
                }
            }

            if (searchquery.Count <= 0 && b.casualspeech) // If 'casual speech's etting is on and we've found nothing...
            {
                // This basically repeats it, but rather than checking subject keywords, it just goes through the text of every node. Very loose and rarely works well.
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

            int lala = -1;
            textNode dnode = null;
            // Finally, find the node in our list with the highest priority.
            foreach (KeyValuePair<textNode, int> pair in searchquery)
            {
                if (pair.Value > lala) { dnode = pair.Key; }
            }

            return dnode; // Aaaaaand return that node!
        }
        static List<textNode> GetLinkedNode(botSettings b, textNode t)
        {
            List<textNode> woo = new List<textNode>(); // Create empty list.
            if (t.connections.Count > 0) // If the node in question has connections...
            {
                foreach (int con in t.connections) // For each of those...
                {
                    foreach (textNode q in l.nodes) // For each node...
                    {
                        if ((q._state != state && q._state != -1) || !b.allowednodes.Contains(q._id)) { continue; } // If not allowed to use this node, skip.

                        if (q._id == con && !op.used.Contains(q)) // If the node equals the connection we're looking for...
                        {
                            woo.Add(q); // Add to our list.
                        }
                    }
                }
            }
            return woo;
        }
        static List<textNode> GetDependentNode(botSettings b, UUID a, textNode t)
        {
            int i = 0;
            List<textNode> woo = new List<textNode>(); // Create empty list.
            if (t.dependencies.Count > 0)  // If the node in question has dependencies...
            {
                foreach (int con in t.dependencies) // For each of those...
                {
                    i = 0;
                    foreach (textNode q in l.nodes) // For each node...
                    {
                        bool no = false;
                        if ((q._state != state && q._state != -1) || !b.allowednodes.Contains(q._id)) { continue; } // If not allowed to use this node, skip.
                        if (s.mPeople.IndexOf(a) != -1)
                        {
                            int foo = s.mLearning[s.mPeople.IndexOf(a)][i];
                            if (foo > 0) { no = true; } // They know it; don't bother.
                        }

                        if (!no && q._id == con && !op.used.Contains(q))
                        {
                            woo.Add(q); // Add to our list!
                        }

                        i++;
                    }
                }
            }
            return woo;
        }
        static string ReadNode(string data)
        {
            string newdata = "";
            if (data.Length == 0) { return data; } // Sanity check

            string[] words = data.Split(' ');


            foreach (string word in words)
            {
                if (word.EndsWith(")"))
                {
                    /*
                    // Check for function woo
                    string mc = word;
                    mc = mc.Remove(mc.IndexOf("(")); // Remove arguments

                    string argms = word;
                    argms = argms.Remove(0, argms.IndexOf("(") + 1);
                    argms = argms.Remove(argms.Length - 1);

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
                    Type t = typeof(MockingBOT);
                    t.InvokeMember(mc, BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance, null, null, args);
                    */
                }
                else
                {
                    newdata += word + " ";
                }
            }

            //newdata = String.Join(" ",words);
            return newdata;
        }
        // Filter out a string for keywords, for better searching. 'GetSubject' is for getting the subject of the sentence, but isn't yet finished.
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
                    //string foo = getWordType(s[i]);
                    //if (!generic.Contains(s[i]) && (foo == "Noun" || foo == "Verb" || foo == "Adjective" || foo == "Adverb")) { l.Add(s[i]); }
                    if (!generic.Contains(s[i])) { l.Add(s[i]); }
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
                //if (getWordType(w) == "Noun") { l.Add(w); }
            }
            return l;
        }

        // Functions for making a bot chat.
        static void BotSpeak(UUID av, Scene sc, string str)
        {
            botOperator o = npcop[bots.IndexOf(av)];
            if (o.shutup == true) { return; }
            if (o.flooddetect == 0)
            {
                UUID typing = new UUID("c541c47f-e0c0-058b-ad1a-d6ae3a4584d9");
                DoAnimation(av, typing);
                Thread.Sleep(1000 + str.Split(' ').Length * 100);
                bot.Say(av, sc, str);
                if (tts) { Speech.Speak(str); }
                StopAnimation(av, typing);
            }
            o.timesmsgd++; 
        }
        static void BotSpeak(UUID av, Scene sc, string str, bool typeanim)
        {
            botOperator o = npcop[bots.IndexOf(av)];
            if (o.shutup == true) { return; }
            if (o.flooddetect == 0)
            {
                UUID typing = new UUID("c541c47f-e0c0-058b-ad1a-d6ae3a4584d9");
                if (typeanim == true)
                {
                    DoAnimation(av, typing);
                    Thread.Sleep(300 + str.Split(' ').Length * 10);
                }
                bot.Say(av, sc, str);
                if (typeanim == true) { StopAnimation(av, typing); } //Client.Self.AnimationStop(typing, true); }
                if (tts) { Speech.Speak(str); }
            }
            o.timesmsgd++;
        }
        static void BotSpeak(NPCAvatar av, string str)
        {
            m_log.Debug(av.FirstName + " says: " + str);

            botOperator o = npcop[npcavatars.IndexOf(av)];
            if (o.shutup == true) { return; }
            if (o.flooddetect == 0)
            {
                UUID typing = new UUID("c541c47f-e0c0-058b-ad1a-d6ae3a4584d9");
                DoAnimation(av.AgentId,typing);
                Thread.Sleep(800 + str.Split(' ').Length * 60);
                av.Say(str);
                if (tts) { Speech.Speak(str); }
                StopAnimation(av.AgentId, typing);
            }
            o.timesmsgd++;
        }
        static void BotSpeak(NPCAvatar av, string str, bool typeanim)
        {
            m_log.Debug(av.FirstName + " says: " + str);

            botOperator o = npcop[npcavatars.IndexOf(av)];
            o = npcop[npcavatars.IndexOf(av)];
            if (o.shutup == true) { return; }
            if (o.flooddetect == 0)
            {
                UUID typing = new UUID("c541c47f-e0c0-058b-ad1a-d6ae3a4584d9");
                if (typeanim == true)
                {
                    //Client.Self.AnimationStart(typing, true);
                    DoAnimation(av.AgentId, typing);
                    Thread.Sleep(800 + str.Split(' ').Length * 60);
                }
                av.Say(str);
                if (tts) { Speech.Speak(str); }
                if (typeanim == true) { StopAnimation(av.AgentId, typing); }
            }
            o.timesmsgd++;
        }        
        // Functions for making a bot Instant Message.
        static void BotIM(NPCAvatar av, UUID t, string str)
        {
            Thread.Sleep(800 + str.Split(' ').Length * 60);
            IClientAPI c;
            if (clients.TryGetValue(t, out c)) { c.SendInstantMessage(new GridInstantMessage(getScene(av.AgentId), av.AgentId, getName(av.AgentId), t, 0, str,false, getPosition(av.AgentId)));}
        }
        static void BotIM(NPCAvatar av, UUID t, string str, UUID session)
        {
             Thread.Sleep(800 + str.Split(' ').Length * 60);
             IClientAPI c;
             if (clients.TryGetValue(t, out c)) { c.SendInstantMessage(new GridInstantMessage(getScene(av.AgentId), av.AgentId, getName(av.AgentId), t, 0, false, str, session, false, getPosition(av.AgentId), new byte[]{0,0}, true)); }
        }

        // Get height of a position in the region.
        static float getHeight(Vector3 pos, Scene sc)
        {
            return sc.GetGroundHeight(pos.X, pos.Y); //Client.Network.CurrentSim.TerrainHeightAtPoint((int)newdest.X, (int)newdest.Y, out butt);
        }

        // The routines for greeting a person...
        private static void greet(NPCAvatar b, UUID av)
        {
            botOperator o = npcop[npcavatars.IndexOf(b)];
            botSettings s = l.bots[npcid[npcavatars.IndexOf(b)]];
            if (o.shutup == true) { return; }
            o.greetlist.Add(av); // Adds them to the list of people we've greeted, so this doesn't happen over and over.

            if (s.mPeople.IndexOf(av) != -1) // If known...
            {
                if (s.mRelationship[s.mPeople.IndexOf(av)] == 0) // If no relationship...
                {
                    BotSpeak(b, String.Format(getSpeech(0), getName(av))); // "Hello, {0}!"
                }
                else // If there IS a relationship...
                {
                    BotSpeak(b, String.Format("Hello again, {0}!", getName(av))); // I remember you! "Hello again, {0}!"
                }
            }
            else { BotSpeak(b, String.Format(getSpeech(0), getName(av))); } // Unknown person. Obviously, not remembered. "Hello, {0}!"
        }
        // ...And for asking to follow then.
        private static void askToFollow(NPCAvatar b, UUID av)
        {
            botOperator o = npcop[npcavatars.IndexOf(b)];
            botSettings s = l.bots[npcid[npcavatars.IndexOf(b)]];
            if (o.shutup == true) { return; }

            BotSpeak(b, getSpeech(1)); // 'May I follow you?'
            if (waitForAnswer(av, false) == true) // Wait for a 'yes' or 'no'. This effectively PAUSES this method forever, until they hear a 'yes' or 'no', OR, when it times out.
            {
                // If yes...
                BotSpeak(b, getSpeech(2)); // Okay!
                o.following = true; // Is now in follow mode.
                o.tofollow = av; // Following this person.
            }
            else { BotSpeak(b, getSpeech(3)); } // If no.
        }

        // These pause a given method by making a (while) loop and makes the bot listen for a 'yes' or 'no'. Thus, have it ask the question FIRST. Then, run this.
        // This will 'hang' whatever thread it's in, so be careful.
        // Note that it takes the possible variations of 'yes' or 'no' from a list of affirmative or negatory responses within ChatFromClients.
        private static bool waitForAnswer(UUID av, bool def)
        {
            int timeout = 0;
            op.yesno = -1; // When it hears a 'yes' or 'no', this is the variable that gets changed. -1 is 'neither'.
            while (true)
            {
                System.Threading.Thread.Sleep(1);
                timeout++;
                if (op.yesno != -1) // We heard one!
                {
                    int response = op.yesno;
                    op.yesno = -1; // Reset it.
                    if (response == 0) { return false; } // 'No'.
                    else { return true; } // 'Yes'.
                }
                if (timeout > 15000) { return def; } // Timed out -- return default.
            }
        }
        private static bool waitForAnswer(bool def)
        {
            int timeout = 0;
            op.yesno = -1;
            op.waiting = true;
            while (true)
            {
                System.Threading.Thread.Sleep(1);
                timeout++;
                if (op.yesno != -1)
                {
                    m_log.DebugFormat("Heard response: {0}", op.yesno.ToString());
                    int response = op.yesno;
                    op.yesno = -1;
                    if (response == 0) { op.waiting = false; return false; }
                    else { op.waiting = false; return true; }
                }
                if (timeout > 15000) { op.waiting = false; return def; }
            }
        }
        private static string waitForAnswer(int w, UUID av, string def)
        {
            string foo = "";
            if (w == 0) { foo = op.lastresp; }
            else { foo = op.lastIM; }
            int timeout = 0;
            op.yesno = -1;
            op.waiting = true;
            while (true)
            {
                System.Threading.Thread.Sleep(1);
                timeout++;
                if (w == 0)
                {
                    if (op.lastheard == av && op.lastresp != foo)
                    {
                        op.waiting = false;
                        return op.lastresp;
                    }
                }
                else
                {
                    if (op.lastIMer == av && op.lastIM != foo)
                    {
                        op.waiting = false;
                        return op.lastIM;
                    }
                }
                if (timeout > 15000) { op.waiting = false; return def; }
            }
        }

        // Various questions asked that demand resposnes from the user.
        private static void askForStory(UUID av)
        {
            op.currentstory = new botStories();
            l.stories.Add(op.currentstory);
            op.currentstory._id = l.stories.Count;
            op.currentstory._title = "Default";

            op.listening = true;
            //BotSpeak(getSpeech(7));
            op.currentstory._title = getName(av);
        }
        public static void askToTour(UUID b, Scene sc, UUID av)
        {
            BotSpeak(b, sc, getSpeech(70), true);
            if (waitForAnswer(false) == true)
            {
                if (l.tours.Count > 0)
                {
                    if (op.currenttour == null)
                    {
                        int tourgrab = rnd.Next(l.tours.Count);
                        op.currenttour = l.tours[tourgrab];
                    }
                    op.tourist = av;
                    goToTour(b, sc);
                }
                else
                {
                    BotSpeak(b, sc, getSpeech(72));
                }
            }
            else
            {
                BotSpeak(b, sc, getSpeech(71));
            }
        }
        public static void askToTour(NPCAvatar b, UUID av)
        {
            BotSpeak(b, getSpeech(70), true);
            if (waitForAnswer(false) == true)
            {
                if (l.tours.Count > 0)
                {
                    if (op.currenttour == null)
                    {
                        int tourgrab = rnd.Next(l.tours.Count);
                        op.currenttour = l.tours[tourgrab];
                    }
                    op.tourist = av;
                    goToTour(b);
                }
                else
                {
                    BotSpeak(b, getSpeech(72));
                }
            }
            else
            {
                BotSpeak(b, getSpeech(71));
            }
        }

        // For beginning a story by taking it from a list.
        private static void tellStory(NPCAvatar b, UUID av)
        {
            if (l.stories.Count > 0)
            {
                if (op.currentstory == null)
                {
                    int storygrab = rnd.Next(l.stories.Count);
                    op.currentstory = l.stories[storygrab];
                }
                op.storytelling = true;
                op.listener = av;
            }
            else
            {
                BotSpeak(b, getSpeech(15));
            }
        }

        // For finding a tour location and going there.
        private static void goToTour(UUID b, Scene sc)
        {
            if (op.currenttour.tourLocs.Count > op.tourStage)
            {
                //BotIM(op.tourist, "This way~");
                op.tourLoc = op.currenttour.tourLocs[op.tourStage];
                op.touring = true;
                op.touristPresent = false;
                op.tstoryStage = 0;
                BotSpeak(b, sc, getSpeech(17));
            }
            else
            {
                //BotIM(op.tourist, "And that is the end of the tour.");
                op.touring = false;
                op.tstoryStage = 0;
                BotSpeak(b, sc, getSpeech(19));
            }
        }
        private static void goToTour(NPCAvatar b)
        {
            if (op.currenttour.tourLocs.Count > op.tourStage)
            {
                //BotIM(op.tourist, "This way~");
                op.tourLoc = op.currenttour.tourLocs[op.tourStage];
                op.touring = true;
                op.touristPresent = false;
                op.tstoryStage = 0;
                BotSpeak(b, getSpeech(17));
            }
            else
            {
                //BotIM(op.tourist, "And that is the end of the tour.");
                op.touring = false;
                op.tstoryStage = 0;
                BotSpeak(b, getSpeech(19));
            }
        }

        // For... asking to teleport someone to them.
        private static void askToTeleport(UUID b, UUID person)
        {
            IClientAPI c;
            IClientAPI c2;
            clients.TryGetValue(person, out c2);
            if (clients.TryGetValue(person, out c))
            {
                OpenSim.Region.CoreModules.Avatar.Lure.LureModule lm = new OpenSim.Region.CoreModules.Avatar.Lure.LureModule();
                if (op.senttele == false) {
                    Scene scene = getScene(b);
                    ScenePresence presence = sP(b);
                    UUID dest = Util.BuildFakeParcelID(
                    scene.RegionInfo.RegionHandle,
                    (uint)presence.AbsolutePosition.X,
                    (uint)presence.AbsolutePosition.Y,
                    (uint)presence.AbsolutePosition.Z);
                    c.SendInstantMessage(new GridInstantMessage(scene, b, getName(b), person, (byte)InstantMessageDialog.RequestTeleport, false, "Would you like to come join me?", dest, false, sP(b).AbsolutePosition, new byte[]{0}, false));
                    op.senttele = true; 
                }
            }
        }

        // These are used for when we're waiting for a tourist, to send them teleport requests, etc.
        public static void waitForTourist(UUID b, UUID t)
        {
            if (op.touristPresent == false)
            {
                Vector3 pos = getPosition(b);
                Vector3 pos2 = getPosition(t);

                if (Vector3.Distance(pos, pos2) <= 5) // Person is there.
                {
                    //BotIM(t, "Hello!");
                    op.touristPresent = true;
                    if (op.tourWait > 5) { BotSpeak(b, getScene(b), getSpeech(21)); }
                    else { BotSpeak(b, getScene(b), getSpeech(20)); }
                }

                if (op.tourWait == 20)
                {
                    askToTeleport(b,t);
                }

                if (op.tourWait == 30)
                {
                    /*
                    BotIM(t, getSpeech(22));
                    if (waitForAnswer(t, true) == false)
                    {
                        op.tourWait = 0;
                        BotIM(t, getSpeech(23));
                    }
                    else
                    {
                        BotIM(t, getSpeech(24));
                        op.touring = false;
                    }
                    */
                }
            }
        }
        public static void waitForTourist(NPCAvatar b, UUID t)
        {
            if (op.touristPresent == false)
            {
                Vector3 pos = b.Position; 
                Vector3 pos2 = getPosition(t);

                if (Vector3.Distance(pos, pos2) <= 5) // Person is there.
                {
                    //BotIM(t, "Hello!");
                    op.touristPresent = true;
                    if (op.tourWait > 5) { BotSpeak(b, getSpeech(21)); }
                    else { BotSpeak(b, getSpeech(20)); }
                }

                if (op.tourWait == 20)
                {
                    askToTeleport(b.AgentId, t);
                }

                if (op.tourWait == 30)
                {
                    /*
                    BotIM(t, getSpeech(22));
                    if (waitForAnswer(t, true) == false)
                    {
                        op.tourWait = 0;
                        BotIM(t, getSpeech(23));
                    }
                    else
                    {
                        BotIM(t, getSpeech(24));
                        op.touring = false;
                    }
                    */
                }
            }
        }

        // These check Wikipedia for a random paragraph and then spit it out. Very buggy at the moment.
        public static void BotTrivia(NPCAvatar b, string woo)
        {
            string trivia = getWikipedia(woo);
            if (trivia.Length > 0) // Sanity check
            {
                // 'Flavatext' is just for fun and to add some personality.
                string flavatext = "";
                int ran = rnd.Next(5);
                if (ran == 0) { flavatext = "Here's some trivia for ya: "; }
                else if (ran == 1) { flavatext = "Here's a fun fact! "; }
                else if (ran == 2) { flavatext = "Wanna hear some trivia? Too bad. "; }
                else if (ran == 3) { flavatext = "Let me look up the Wikipedia on this place! "; }
                else if (ran == 4) { flavatext = "Excuse me while I reach into the Internets to tell you more of this magical land. "; }
                BotSpeak(b, flavatext + trivia);
            }
        }

        // Miscellaneous linguistic things that the bot checks for and responds to.
        public static void handleDialogue(NPCAvatar b, UUID av, string foo)
        {
            string[] bar = foo.Split(' '); // For detecting lone words by their lonely lonesome (so 'you' doesn't trigger the check for 'yo', for example).

            if (foo.Contains("stop following") || foo.Contains("don't follow") || foo.Contains("go away"))
            {
                if (op.following == true)
                {
                    op.following = false;
                    BotSpeak(b, getSpeech(5));
                }
                else
                {
                    BotSpeak(b, getSpeech(73));
                }
            }
            else if (bar.Contains("follow") || foo.Contains("come with")) // To-do: 'Go with X' to make it follow another person.
            {
                if (op.following == false)
                {
                    op.following = true; op.tofollow = av;
                    BotSpeak(b, getSpeech(2));
                }
                else
                {
                    BotSpeak(b, getSpeech(4));
                }
            }

            if (foo.Contains("shut up") || foo.Contains("be quiet") || foo.Contains("don't talk"))
            {
                BotSpeak(b, getSpeech(74));
                op.shutup = true;
            }
            if (foo.Contains("you can talk") || foo.Contains("you can speak"))
            {
                op.shutup = false;
                BotSpeak(b, getSpeech(41));
            }


            if (foo.Contains("fuck you") || foo.Contains("screw you") || foo.Contains("i hate you") || foo.Contains("you suck"))
            {
                BotSpeak(b, getSpeech(45));
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
                BotSpeak(b, lol);
            }
        }
        public static void handleDialogue(NPCAvatar b, UUID av, string foo, UUID session)
        {
            string[] bar = foo.Split(' '); // For detecting lone words by their lonely lonesome (so 'you' doesn't trigger the check for 'yo', for example).

            if (foo.Contains("stop following") || foo.Contains("don't follow") || foo.Contains("go away"))
            {
                if (op.following == true)
                {
                    op.following = false;
                    BotIM(b, av, getSpeech(5), session);
                }
                else
                {
                    BotIM(b, av, getSpeech(73), session);
                }
            }
            else if (bar.Contains("follow") || foo.Contains("come with")) // To-do: 'Go with X' to make it follow another person.
            {
                if (op.following == false)
                {
                    op.following = true; op.tofollow = av;
                    BotIM(b, av, getSpeech(2), session); ;
                }
                else
                {
                    BotIM(b, av, getSpeech(4), session);
                }
            }

            if (foo.Contains("shut up") || foo.Contains("be quiet") || foo.Contains("don't talk"))
            {
                BotIM(b, av, getSpeech(74), session);
                op.shutup = true;
            }
            if (foo.Contains("you can talk") || foo.Contains("you can speak"))
            {
                op.shutup = false;
                BotIM(b, av, getSpeech(41), session);
            }


            if (foo.Contains("fuck you") || foo.Contains("screw you") || foo.Contains("i hate you") || foo.Contains("you suck"))
            {
                BotIM(b, av, getSpeech(45), session);
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
                BotIM(b, av, lol, session);
            }
        }

        // For animating.
        public static void DoAnimation(UUID b, UUID a)
        {
            if (sP(b).Animator.Animations.HasAnimation(a)) { sP(b).Animator.RemoveAnimation(a, false); }
            sP(b).Animator.AddAnimation(a, b);
        }
        public static void StopAnimation(UUID b, UUID a)
        {
            sP(b).Animator.RemoveAnimation(a, false);
        }

        // Checking if someone is on the access list (or if we're in public mode).
        private static bool hasAccess(UUID av)
        {
            if (!s.privateaccess) { return true; }

            if (s.alist.Contains(av)) { return true; }
            else { return false; }
        }

        // These basically just spit out a randomized Vector position. The first and second are used for hhdest and only that. The others can be used to send out to any other variable.
        private static void randomizeDestination()
        {
            Vector3 newdest = new Vector3();
            newdest.X = rnd.Next(255); newdest.Y = rnd.Next(255);
            float h = m_scenes[0].GetGroundHeight(newdest.X, newdest.Y); //Client.Network.CurrentSim.TerrainHeightAtPoint((int)newdest.X, (int)newdest.Y, out butt);
            newdest.Z = h;
            if (h != 0) { op.hhdest = newdest; } //op.hhcity = GetWhere(op.hhdest); }
        }
        private static void randomizeDestination(UUID av)
        {
            Vector3 newdest = new Vector3();
            newdest.X = rnd.Next(255); newdest.Y = rnd.Next(255);
            float h = getScene(av).GetGroundHeight(newdest.X, newdest.Y); //Client.Network.CurrentSim.TerrainHeightAtPoint((int)newdest.X, (int)newdest.Y, out butt);
            newdest.Z = h;
            if (h != 0) { op.hhdest = newdest; } //op.hhcity = GetWhere(op.hhdest); }
        }
        private static void randomizeDestination(out Vector3 whee)
        {
            whee = Vector3.Zero;
            Vector3 newdest = new Vector3();
            newdest.X = rnd.Next(255); newdest.Y = rnd.Next(255);
            float h = m_scenes[0].GetGroundHeight(newdest.X, newdest.Y); //Client.Network.CurrentSim.TerrainHeightAtPoint((int)newdest.X, (int)newdest.Y, out butt);
            newdest.Z = h;
            if (h != 0) { whee = newdest; }
        }
        private static void randomizeDestination(Scene sc, out Vector3 whee)
        {
            whee = Vector3.Zero;
            Vector3 newdest = new Vector3();
            newdest.X = rnd.Next(255); newdest.Y = rnd.Next(255);
            float h = sc.GetGroundHeight(newdest.X, newdest.Y); //Client.Network.CurrentSim.TerrainHeightAtPoint((int)newdest.X, (int)newdest.Y, out butt);
            newdest.Z = h;
            if (h != 0) { whee = newdest; }
        }

        // Pretty self-explanatory.
        private static UUID FindClosestAvatar(UUID b)
        {
            OpenMetaverse.Vector3 mypos = sP(b).OffsetPosition;
            OpenMetaverse.Vector3 otherpos = new Vector3();

            UUID mar = new UUID();
            float chec = new float();
            chec = 0;
            List<UUID> ppl = new List<UUID>();
            List<Vector3> locas = new List<Vector3>();
            Vector3 pos = sP(b).OffsetPosition;
            getScene(b).SceneGraph.GetCoarseLocations(out locas, out ppl, 99);
            for (int i = 0; i < ppl.Count; i++)
            {
                if (ppl[i] != sP(b).UUID)
                {
                    otherpos = locas[i];
                    if (Vector3.Distance(mypos, otherpos) < chec || chec == 0)
                    {
                        chec = Vector3.Distance(mypos, otherpos);
                        mar = ppl[i];
                    }
                }
            }
            return mar;
        }

        // For memory - remembering folks, and whatever information they've been given.
        static void rememberPerson(botSettings bs, UUID av, int relate)
        {
            if (!bs.mPeople.Contains(av)) { bs.mPeople.Add(av); bs.mRelationship.Add(0); bs.mLearning.Add(new List<int>()); } // Add to our 'array' of lists.
            int place = bs.mPeople.IndexOf(av); // I thiiiiiink this is completely safe? 100%? I get the strange feeling there's something up with it, but w/e.
            bs.mRelationship[place] = relate;
        }
        static void rememberTaught(botSettings bs, UUID av, int node)
        {
            if (!bs.mPeople.Contains(av)) { bs.mPeople.Add(av); bs.mRelationship.Add(0); bs.mLearning.Add(new List<int>()); } // Add to our 'array' of lists.
            int place = bs.mPeople.IndexOf(av); // I thiiiiiink this is completely safe? 100%? I get the strange feeling there's something up with it, but w/e.
            bs.mLearning[place][node] = 1; // Person knows this thing now woo
        }

        // Linguistically checking if this is a question or not.
        static bool isQuestion(string str)
        {
            str = str.ToLower();
            if (str.EndsWith("?")) { return true; } // No brainer.
            string[] s = str.Split(' ');
            int likelihood = 0;

            for (int i = 0; i < s.Length; i++)
            {
                if (i < 4)
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
                        if (s.Length > i + 1) { if (s[i] == "I" && s[i + 1] == "would") { likelihood += 30; } }
                        if (s.Length > i + 2) { if (s[i] == "tell" && s[i + 1] == "me") { likelihood += 30; } } // Likewise.
                    }

                    if (s[i] == "who" || s[i] == "when" || s[i] == "what" || s[i] == "why" || s[i] == "where" || s[i] == "how")
                    {
                        likelihood += 10;
                    }
                }
            }
            if (str.EndsWith(".") || str.EndsWith(",") || str.EndsWith("!")) { likelihood -= 10; } // While netspeakers may not include a question mark, another form of punctuation is more obviously not a question.

            if (likelihood > 5) { return true; }

            return false;
        }
        static bool isQuestion(string str, UUID botasked)
        {
            str = str.ToLower();
            if (str.EndsWith("?")) { return true; } // No brainer.
            string[] s = str.Split(' ');
            int likelihood = 0;

            for (int i = 0; i < s.Length; i++)
            {
                if (i < 4)
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
                        if (s.Length > i + 1) { if (s[i] == "I" && s[i + 1] == "would") { likelihood += 30; } }
                        if (s.Length > i + 2) { if (s[i] == "tell" && s[i + 1] == "me") { likelihood += 30; } } // Likewise.
                    }

                    if (s[i] == "who" || s[i] == "when" || s[i] == "what" || s[i] == "why" || s[i] == "where" || s[i] == "how")
                    {
                        likelihood += 10;
                    }
                    if (s[i] == getName(botasked))
                    {
                        if (s.Length > i + 1) { if (s[i + 1] != "is" && s[i + 1] != "can") { likelihood += 10; } }
                        else { likelihood += 10; }
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
            //if (isQuestion(str) == true) { return false; } // Questions a'int statements.
            return true;
        }
        // Dividing things into compound sentences.
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
                    else if (whee[i].EndsWith(","))
                    {
                        // There's a bajillion ways to use commas. Filter it out here.
                        bool yay = false;

                        // 'X, X, and X' / 'X, X, but X' / etc.
                        for (int ii = i; ii < whee.Length - 1; ii++) { if (conjunctions.Contains(whee[ii + 1]) && whee[ii].EndsWith(",")) { yay = true; } }

                        if (yay == true) { cut = i + 1; }
                    }
                }

                if (cut != -1)
                {
                    char punc = ' ';
                    for (int ii = last; ii < whee.Length; ii++)
                    {
                        if (char.IsPunctuation(whee[ii].Last()) && whee[ii].Last() != ',')
                        { punc = whee[ii].Last(); break; }
                    }

                    string w = "";
                    for (int ii = last; ii < cut; ii++) { w += whee[ii] + " "; }
                    w = w.Trim();
                    if (char.IsPunctuation(w.Last())) { w = w.Remove(w.Length - 1); }
                    w += punc; // Adds the punctuation from the end of the sentence.

                    c.Add(w);
                    last = i + 1 + skip;
                }

                // End. Finish up by adding the remainder.
                if (i == whee.Length - 1) { string w = ""; for (int ii = last; ii <= i; ii++) { w += whee[ii] + " "; } w = w.Trim(); c.Add(w); }
            }
            return c;
        }
        // Making things more machine-readable by clearing up contractions, translating netspeak, etc.
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
                "rl","stfu","tbh","tmi","ttyl","ty","wb","w/e","w8","wtf","yw","ur","bby" };

            List<string> english = new List<string> { "you", "are", "why","as far as i know","away from keyboard","age sex location","be right back","by the way","be back in a bit",
                "be back later","see you","see you","for fuck's sake","for the win","fuck you","for your information","good going","good job","get the fuck out","got to go","got to go",
                "great","i don't care","i don't know","if i recall correctly","in my opinion","in my honest opinion","in real life","i want sex now","just kidding","just kidding",
                "just fucking google it","okay","okay","okay thanks","okay thanks bye","okay thanks bye","let me know","anyone","no problem","not safe for work","nevermind","oh really",
                "oh i see","oh my god","oh my fucking god","point of view","people","real life","shut the fuck up","to be honest","too much information","talk to you later","thank you","welcome back",
                "whatever","wait","what the fuck","you're welcome","your","baby"};

            //if (contraction.Count != simplified.Count) { MessageBox.Show("Warning"); }
            //if (netspeak.Count != english.Count) { MessageBox.Show("Warning"); }

            string[] f = s.Split(' ');
            s = "";
            for (int i = 0; i < f.Length; i++)
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

        // Save/Load
        public void SaveSettings(string path)
        {
            XmlSerializer sav = new XmlSerializer(typeof(BotStorage));
            using (var stream = new FileStream(path, FileMode.Create))
            {
                sav.Serialize(stream, l);
                stream.Close();
            }
        }
        public void LoadSettings(string path)
        {
            object obj = new object();
            try
            {
                XmlSerializer d = new XmlSerializer(typeof(BotStorage));
                using (var stream = new FileStream(path, FileMode.Open))
                {
                    try
                    {
                        l = (BotStorage)d.Deserialize(stream);

                        m_log.DebugFormat("[MockingBOT]: Loading successful.");
                    }
                    catch
                    {
                        m_log.DebugFormat("[MockingBOT]: Loading failed.");
                    }
                }
            }
            catch
            {
                m_log.DebugFormat("[MockingBOT]: Loading failed.");
            }
        }

        // Getting speech strings.
        private static string getSpeech(int g)
        {
            string str = "";
            try { str = s.speech[s.currentspeech][g]; } // See if the bot has it set.
            catch { 
                // If not, here are the default strings...
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
                else if (g == 39) { return "Pardon?"; }
                else if (g == 40) { return "Great!"; }
                else if (g == 41) { return "Okay!"; }
                else if (g == 42) { return "All right."; }
                else if (g == 43) { return "All right!"; }
                else if (g == 44) { return "Oh, okay..."; }
                else if (g == 45) { return "Arright, no problem."; }
                else if (g == 46) { return ":("; }
                else if (g == 47) { return "Thank you!"; }
                else if (g == 48) { return ""; }
                else if (g == 49) { return ""; }
                else if (g == 50) { return ""; }
                else if (g == 51) { return ""; }
                else if (g == 52) { return ""; }
                else if (g == 53) { return ""; }
                else if (g == 54) { return ""; }
                else if (g == 55) { return ""; }
                else if (g == 56) { return ""; }
                else if (g == 57) { return ""; }
                else if (g == 58) { return ""; }
                else if (g == 59) { return ""; }
                else if (g == 60) { return "Wanna hear a story?"; }
                else if (g == 61) { return "Thank you! This is where I wanted to be."; }
                else if (g == 62) { return "Take care!"; }
                else if (g == 63) { return "...Should I finish the story?"; }
                else if (g == 64) { return "/me waves."; }
                else if (g == 65) { return "/me sighs."; }
                else if (g == 66) { return "I am on my way to {0}, which is about {1} miles going {2}."; }
                else if (g == 67) { return "I am on my way to {0}."; }
                else if (g == 68) { return "Hey, we're in {0}."; }
                else if (g == 69) { return "Hello again, {0}!"; }
                else if (g == 70) { return "Would you like a tour?"; }
                else if (g == 71) { return "Arright. Come talk to me if you change your mind."; }
                else if (g == 72) { return "No tours available."; }
                else if (g == 73) { return "I wasn't following you."; }
                else if (g == 74) { return "Shutting up."; }
                else if (g == 75) { return "Tweeted!"; }
                else if (g == 76) { return "That's too long to tweet! It was {0} characters - {1} too many!"; }
                else if (g == 77) { return "The date is {0}. Conditions are {1}, with a low of {2} and a high of {3}."; }
                else if (g == 78) { return "May I share it?"; }
                else if (g == 79) { return ""; }
                else if (g == 80) { return "I don't believe that's a proper command."; }
                else { return "";  }
                //str = speech[g]; 
            }
            string[] st = str.Split('|');
            str = st[rnd.Next(st.Length)].Trim();
            return str;
        }

        // Self-explanatory.
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
            double der = (s.Length / (140 - 8));
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
                    if (foo[i].Length > 140 - ex.Length) { continue; } // Just ignore words over 140 characters in length. That would be cray.
                    str += foo[i] + " ";
                    if (str.Length > 140 - ex.Length) { str = old; at = i; break; }
                    if (i == foo.Length - 1) { bar = false; break; }
                }
                using (twit) { twit.UpdateStatus(str); }
            }
        }

        // Geolocating stuff.
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

        // Get the 'Where On Earth ID' Yahoo uses for stuff.
        public static string GetWoeid(string Zipcode)
        {
            string query = String.Format("http://where.yahooapis.com/v1/places.q('{0}')?appid={1}", Zipcode, "WindowsFormsApplication1.MockingBOT"); //System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            XDocument thisDoc = XDocument.Load(query);
            XNamespace ns = "http://where.yahooapis.com/v1/schema.rng";

            return (from i in thisDoc.Descendants(ns + "place") select i.Element(ns + "woeid").Value).First();
        }
        public static string GetWoeid(string lat, string lon)
        {
            string query = String.Format("http://query.yahooapis.com/v1/public/yql?q=select * from geo.placefinder where text = %22{0},{1}%22 and gflags = %22R%22", lat, lon);
            XDocument thisDoc = XDocument.Load(query);
            XNamespace ns = "http://where.yahooapis.com/v1/base.rng";

            return (from i in thisDoc.Descendants("Result") select i.Element("woeid").Value).First();
        }
        // Get Weather (requires WOEID). I believe Yahoo is fine with this API being used.
        public static void getWeather(string woeid, out List<string> weather)
        {
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

        // For handling lists in various ways.
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
        public static void SwapItems(List<string> l, int a, int b)
        {
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

        // When finished, this would allow a bot to learn any string of data and save it as a node.
        public static void LearnString(string str)
        {
            // This takes a string of information and adds it to the database of nodes she can pull up, complete with keywords, etc.
            //if (!isStatement(str)) { MessageBox.Show("Not a statement."); return; }
            List<string> keywords = getKeywords(str);

            textNode biscuit = new textNode();
            l.nodes.Add(biscuit);
            biscuit._id = l.nodes.Count;
            biscuit._width = 100;
            biscuit._height = 30;
            biscuit._state = -1;
            biscuit._x = 0;
            biscuit._y = 0;

            for (int i = 0; i < keywords.Count; i++) { biscuit._subject += keywords[i] + " "; }
            biscuit._data = str;
        }

        // Gets Wikipedia articles and attempts to filter the JSON crap out into human-readable text.
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
                    //int start = 0;
                    //int end = 0;
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

                            bracketed = next.Substring(yay + 1, woo - (2 + yay));
                            //MessageBox.Show(bracketed);

                            str = str.Remove(i, woo); // Cut that shit!

                            if (bracketed.Contains("|"))
                            {
                                bracketed = bracketed.Remove(0, bracketed.IndexOf("|") + 1);
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

                    if (i >= str.Length - 1 || i < 0) { break; }
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
                    result[0] = result[0].Remove(0, result[0].IndexOf('['));
                    result[0] = result[0].Replace("[", "");
                    result[0] = result[0].Replace("]", "");
                    search = result[0]; // This will refrain from moving on, restarting the query, but with the redirect URL instead.
                }
                else { break; }

                if (trycount >= 3) { break; }
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

                if (bloop.Contains("'''") || (alphan > nonalphan && !bloop.Contains("|"))) // "|" is usually op.used in code strings.
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

        // Sends bots to a location using AutoPilot.
        private static void goTo(UUID av, Vector3 pos)
        {
            botOperator o = npcop[bots.IndexOf(av)];
            if (o.currentdest == pos) { if (sP(av).Velocity != Vector3.Zero) return; }

            bot.StopMoveToTarget(av, getScene(av));
            o.currentdest = pos;
            float h = getScene(av).GetGroundHeight(pos.X, pos.Y);
            bool nofly = true;
            bool land = true;
            if (pos.Z > h + 5) { nofly = false; land = false; }

            bot.MoveToTarget(av, getScene(av), pos, nofly, land, false);
        }
        private static void goTo(NPCAvatar av, Vector3 pos)
        {
            botOperator o = npcop[npcavatars.IndexOf(av)];
            if (o.currentdest == pos) { if (sP(av.AgentId).Velocity != Vector3.Zero) return; }

            Scene sc = (Scene)av.Scene;
            bot.StopMoveToTarget(av.AgentId, sc);
            o.currentdest = pos;
            float h = sc.GetGroundHeight(pos.X, pos.Y);
            bool nofly = true;
            bool land = true;
            if (pos.Z > h + 5) { nofly = false; land = false; }

            bot.MoveToTarget(av.AgentId, sc, pos, nofly, land, false);
        }
        private static void goTo(NPCAvatar av, Scene sc, Vector3 pos)
        {
            botOperator o = npcop[npcavatars.IndexOf(av)];

            bot.StopMoveToTarget(av.AgentId, sc);
            o.currentdest = pos;
            float h = sc.GetGroundHeight(pos.X, pos.Y);
            bool nofly = true;
            bool land = true;
            if (pos.Z > h + 5) { nofly = false; land = false; }

            bot.MoveToTarget(av.AgentId, sc, pos, nofly, land, false);
        }

        // Find users near a bot, within chat radius.
        public static List<NPCAvatar> getListeners(UUID speaker)
        {
            List<NPCAvatar> foo = new List<NPCAvatar>();
            foreach (NPCAvatar b in npcavatars)
            {
                if (b.Scene == getScene(speaker))
                {
                    //m_log.DebugFormat("[MockingBOT]: NPC at {0} comparing to speaker at {1}", b.Position, getPosition(speaker));
                    if (Vector3.Distance(b.Position, getPosition(speaker)) <= 20) { foo.Add(b); }
                }
            }
            //m_log.Debug("[MockingBOT]: " + foo.Count.ToString() + " bots heard that.");
            return foo;
        }
        
        // For finding various bits of information when needed, since OpenSim's framework is kind of a maze, and things sometimes aren't readily available.
        public static string getName(UUID av)
        {
            return sP(av).Name;
        }
        public static AvatarAppearance getAppearance(UUID av)
        {
            return sP(av).Appearance;
        }
        public static Scene getScene(UUID av)
        {
            ScenePresence foo;
            if (scenes.TryGetRootScenePresence(av, out foo))
            {
                //m_log.Debug("[MockingBOT]: Found " + av.ToString() + " at scene " + foo.Scene.RegionInfo.RegionName);
                return foo.Scene;
            }
            else
            {
                //m_log.Debug("[MockingBOT]: Couldn't find scene - defaulting to 0. This is really bad!");
                return m_scenes[0];
            }
        }
        public static Vector3 getPosition(UUID av)
        {
            return sP(av).OffsetPosition;
        }
        // Gets ScenePresence, which is useful for various purposes - notably in getting an avatar's velocity.
        public static ScenePresence sP(UUID av)
        {
            ScenePresence foo;
            if (scenes.TryGetRootScenePresence(av, out foo))
            {
                return foo;
            }
            else
            {
                m_log.Debug("Couldn't find ScenePresence for UUID " + av.ToString());
                return null;
            }
        } 
    }
}