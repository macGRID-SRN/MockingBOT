using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;

namespace MockingBOT
{
    // This holds the bots themselves and all their personal data, as well as all the nodes, tours, stories, and routines.
    // This is the class that gets serialized into .xml format, for importing and exporting.
    public class BotStorage
    {
        public List<botSettings> bots = new List<botSettings>();
        public List<textNode> nodes = new List<textNode>();
        public List<botTours> tours = new List<botTours>();
        public List<botStories> stories = new List<botStories>();
        public List<botRoutines> routines = new List<botRoutines>();
        public int _default = 0; // The default bot on startup.
    }

    // This is just a basic class for holding the nodes of dialogue.
    public class textNode
    {
        // These are for drawing the 'mind map'.
        public float _x { get; set; }
        public float _y { get; set; }
        public float _width { get; set; }
        public float _height { get; set; }
        public int _color { get; set; }

        // These hold the actual data the bots will read.
        public int _id { get; set; } // Just an ID.
        public string _data { get; set; } // The text that the user will be told.
        public string _subject { get; set; } // The keywords the bot will search for.
        public int _state { get; set; } // As of now, useless - bots have a 'state', default being 0, and will only read nodes of the same state (or -1). This functionality isn't being used for anything at the moment.
        public List<int> connections = new List<int>(); // Nodes that it's linked to - if a node is linked and there are no dependent nodes (which take priority), the bot may take one of these.
        public List<int> dependencies = new List<int>(); // Dependent nodes, as in, 'necessary information' for understanding this node - the bot checks to see if you know it, and if you do, will tell you immediately.

        public textNode() { }
        public textNode(float X1, float Y1) { }
    }
    // Stories that the bot knows and can share, for the HitchBOT protocol. Open for any other uses, such as NPCs in Virtual Hamilton, etc.
    public class botStories
    {
        public int _id { get; set; }
        public string _title = "New Story"; // No real purpose other than to let the user organize things.
        public List<string> paragraphs = new List<string>();
        public bool tweeted = false; // This is to make sure that each story is only tweeted once, to prevent flooding.
        
        public botStories() { }
    }
    // Tours. I'm hoping this is self-explanatory at this point.
    public class botTours
    {
        public int _id { get; set; }
        public bool enabled { get; set; } // Whether or not the bots can use it - right now, it's kind of obsolete, since you can select which bots are allowed to use a given tour/routine... and you can select 'none'.
        public List<Vector3> tourLocs = new List<Vector3>(); // A list indexing all the locations to go to.
        public List<List<String>> tourInfo = new List<List<String>>(); // All the tour information, indexed for each location, and also indexed by paragraph (hence the List<List<>> structure).
        public string tourName = "New Tour"; // No real purpose other than to let the user organize things.
        
        public botTours() { }
    }
    // Routines. This is just basic shopping lists telling the bot 'go here; do this'.
    public class botRoutines
    {
        public string name = "";
        public int _id { get; set; }
        public bool looped = false; // Whether or not the bot will repeat it over and over again.
        public List<Vector3> routineLocs = new List<Vector3>(); // All the locations to go to.
        public List<int> routinePauses = new List<int>(); // The pauses at those locations.
        public List<UUID> routineAnimations = new List<UUID>(); // The animations at those locations.
        public List<string> routineSpeech = new List<string>(); // The speech strings to recite at those locations.

        public botRoutines() { }
    }

    public class botSettings
    {
        public string name = "New Bot"; // For use in generating the bot via Region Module, not just for organization.
        public string description = ""; // This, however, is just for organization and clarification on user-end -- the bots don't make use of it right now. It could be spoken when asked 'who are you?'
        public UUID homeRegion = UUID.Parse("efb29615-3a13-41a3-b458-cf27af7afaa6");
        public Vector3 homeLoc = Vector3.Parse("<128, 128, 70>");
        public Vector3 idleLoc { get; set; } // Where to go when idling.
        public int moveType = 1; // Movement type 
        public int botType = 0; // Bot type
        public bool privateaccess = false; // Whether or not only those on the access list may make commands of the bot.
        public bool autotweet = false; // Whether or not to automatically tweet new stories.
        public int idleTime = 0;
        public int stuckTime = 0;
        public Vector3 wanderDest = Vector3.Zero;

        // These determine which nodes, tours, etc., the bots are allowed access to. Any not in this list are ignroed when the bot loops through them.
        public List<int> allowednodes = new List<int>();
        public List<int> allowedstories = new List<int>();
        public List<int> allowedroutines = new List<int>();
        public List<int> allowedtours = new List<int>();

        public bool tts = false;
        public bool casualspeech = false; // Whether to use loose rules for searching through nodes or not. Rather unreliable - best stick to keywords.
        public List<List<String>> speech = new List<List<String>>(); // Speech strings, with multiple variants available to each bot, in case individual bots will switch personalities at a later stage.
        public int currentspeech = 0; // What speech index to use.
        public List<string> speechnames = new List<string>(); // Names of each string. Not important for data.
        public List<string> sbindings = new List<string>(); // Names of each string. Not important for data.
        public List<UUID> alist = new List<UUID>(); // Access list.
        public List<string> wordsKnown = new List<string>(); // For removed linguistic features.
        public List<string> wordTypes = new List<string>(); // Ditto.
        public List<UUID> mPeople = new List<UUID>(); // People known.
        public List<int> mRelationship = new List<int>(); // How well they're known.
        public List<List<int>> mLearning = new List<List<int>>(); // What nodes the bot knows they name.
        public List<string> mTwitter = new List<string>(); // For the storage of Twitter names for each individual (or 'N/A' if they refused to give it).
        public botSettings()
        {
            idleLoc = Vector3.Zero;
        }
    }

    // This is for all the miscellaneous variables used in operating bots, stored in a class so they can be easily categorized to each bot.
    // It is separate from everything else to prevent it from being serialized. It exists only on runtime.
    public class botOperator {
        public string name = ""; // For debugging - the name of the bot this is tied to, just for making sure we have the right one.
        public Stack<UUID> alist = new Stack<UUID>(); // Access list

        public bool following = new bool(); // Whether or not this bot is in 'stalker' mode.
        public UUID tofollow = new UUID(); // Who we're following.
        public int followtime = 0; // How long we've been following (for timing whether or not to ask for stories, etc.)
        public List<UUID> greetlist = new List<UUID>(); // People we have greeted.
        public List<UUID> noticelist = new List<UUID>(); // People we have noticed.
        
        public int yesno = -1; // For getting responses.
        public bool senttele = false; // Whether or not a teleport request was sent.
        public bool shutup = false; // Whether or not asked not to send chat messages.

        // For flood detection. Shuts it up if too many messages were sent.
        public int timesmsgd = 0;
        public int flooddetect = 0;
        public int floodcd = 200; 
        public int floodcdt = 200;


        public string currentcity = ""; // For geolocating.

        public Vector3 prevPosition = new Vector3(); // Where it was last step - useful for determining speed, direction, etc.
        public Vector3 currentdest = new Vector3(); // Where going.

        public int movetimeout = 0; // For delaying it moving to the next location during a tour.
        public bool waiting = false; // Whether or not we are waiting for a tourist, etc.
        public int askWait = 0; // How long since it's last asked a question/request of the person it's follower.

        public List<Vector3> wanderDest = new List<Vector3>(); // Where to go when wandering.
        public int idleTime = 0; // Time to spend at destination before walking off again.
        public int stuckTime = 0; // Count up when the avatar is attempting to move, but isn't getting anywhere (therefore, is stuck on a wall or something)

        public List<Vector3> tourLocs = new List<Vector3>(); // Stores the various locations of the tour spots, indexed by stage

        // For recalling the last thing it heard or recieved as an IM.
        public string lastresp = "";
        public UUID lastheard = new UUID();
        public string lastIM = "";
        public UUID lastIMer = new UUID();

        public int limit = 1000; // Limit on characters! Second Life sets this as a hard limit.
        public List<textNode> used = new List<textNode>(); // Nodes it's already used, for the SearchNode function.

        // Various, self-explanatory variables for listening to stories, and telling them.
        public bool listening = false;
        public bool storytelling = false;
        public botStories currentstory = null;
        public int storystage = 0;
        public float storywait = 0; // Waiting until telling the next paragraph.
        public UUID listener = new UUID();
        public bool storywaituntilover = false; // Whether or not it's allowed to finish their story before they say 'goodbye', if hitchhiking and has reached its destination.
        public int listeningtime = 0; // For gauging how long it's been since the user last told part of the story.
        public bool endcheck = false; // For checking if it's the end of the story.

        // Various variables for tourist stuff.
        public bool touring = false; // Giving a tour?
        public int tourWait = 0; // Waiting for someone?
        public Vector3 tourLoc = new Vector3(); // Current location
        public UUID tourist = new UUID(); // Who's the tourist?
        public bool touristPresent = false; // Are they present?
        public int tourStage = 0; // What stage are we on?
        public int tstoryStage = 0; // What stage of the information are we telling?
        public bool learningtour = false; // Are we learning a tour?
        public botTours currenttour = null; // What tour are we recalling (by ID)
        public int tourstageedit = 0; // What stage are we on (when learning)

        public bool learningroutine = false; // Are we learning a routine?
        public int currentroutine = 0; // What routine are we on?
        public int routinestage = -1; // What stage?
        
        public Vector3 hhdest = Vector3.Zero; // Where are we hitchhiking to?
        public string hhcity = ""; // What city?

        // Constructors.
        public botOperator() { }
        public botOperator(string n) { name = n; } // For giving it a name. Mostly for debug purposes.
    }
}
