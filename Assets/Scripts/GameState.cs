﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/**
 * Contains functions for checking or modifying the state of the game
 */
public class GameState : MonoBehaviour
{
    public string openingScene;
    public GameObject cluePrefab; // TEMP. Will probably have full Clues instead of just ClueInfo from the generator (Clue being sprite + info)
    public Animator blackFade;

    public static GameState Get() { return GameObject.FindWithTag("GameRules").GetComponent<GameState>(); }

    private string[] clueRooms = { "Bedroom1", "Bedroom2", "Bedroom3" }; // todo: better way of specifying this? data-drive?
    Dictionary<string, List<ClueInfo>> mCluesInRooms = new Dictionary<string, List<ClueInfo>>();

    public ClueInfo[] mRoundClues = new ClueInfo[3]; // the clue that each person found that round
    PersonState[] mPeople;
    private Sprite[] mNonPlayerHeads;
    public Sprite[] NonPlayersHeads
    {
        get
        {
            if (mNonPlayerHeads == null)
            {
                Sprite[] otherSprites = new Sprite[2];
                int j = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (!mPeople[i].IsPlayer)
                    {
                        otherSprites[j] = mPeople[i].HeadSprite;
                        j++;
                    }
                }
                mNonPlayerHeads = otherSprites;
            }
            return mNonPlayerHeads;
        }
    }


    string[] mPersonRooms = new string[3]; // A list of room-names corresponding to the current location of each person
    string mCurrentRoom; // The room that is currently visible
    private string mPendingRoom;

    [HideInInspector]
    public int PlayerId;
    public PersonState Player {
        get { return mPeople[PlayerId]; }
    }

    private Canvas mUICanvas;

    enum LoadState { NONE, UNLOADING_SCENE, LOADING_SCENE }
    private LoadState mLoadState = LoadState.NONE;
    private AsyncOperation mLoadSceneOperation;

    enum GameStage
    {
        MENU,
        INTRO,
        SEARCH_1,
        COMMUNAL_1,
        SEARCH_2,
        COMMUNAL_2,
        SEARCH_3,
        COMMUNAL_3,
        POLICE,
        REVEAL
    }
    private GameStage mCurrentStage;
    private ClueInfo mStartingClue;

    // Start is called before the first frame update
    void Start()
    {
        UIController.Get().HideUI();
        mCurrentStage = GameStage.MENU;
        
        PlayerId = 0; // todo: randomize?
        List<ClueInfo> cluesToScatter;
        MysteryGenerator.Generate(out mPeople, out mStartingClue, out cluesToScatter);

        // give knowledge
        for(int i = 0; i < 3; ++i)
        {
            mPeople[i].knowledge.AddKnowledge(mStartingClue.GetSentence());
        }

        // scatter clues
        foreach (ClueInfo clue in cluesToScatter)
        {
            int room = (int)Random.Range(0, clueRooms.Length);
            string roomName = clueRooms[room];
            if(!mCluesInRooms.ContainsKey(roomName))
            {
                mCluesInRooms.Add(roomName, new List<ClueInfo>());
            }

            mCluesInRooms[roomName].Add(clue);
        }

        mPersonRooms[0] = openingScene;
        mPersonRooms[1] = openingScene;
        mPersonRooms[2] = openingScene;
        
        PlayerInteraction.Get().GoToRoom(openingScene);
    }

    // Update is called once per frame
    void Update()
    {
        if(mLoadSceneOperation != null && mLoadSceneOperation.isDone)
        {
            switch (mLoadState)
            {
                case LoadState.UNLOADING_SCENE:
                    {
                        LoadPendingRoom();
                        break;
                    }
                case LoadState.LOADING_SCENE:
                    {
                        OnRoomLoaded();
                        break;
                    }
            }
        }

        // TEMP to advance time
        if(Input.GetButton("Submit"))
        {
            if(
                    mCurrentStage == GameStage.SEARCH_1
                ||  mCurrentStage == GameStage.SEARCH_2
                ||  mCurrentStage == GameStage.SEARCH_3
            )
            {
                StartStage(mCurrentStage + 1);
            }
        }
    }
    
    public void OnDialogueDismissed(int btnIdx)
    {
        StartStage(mCurrentStage + 1);
    }

    private void StartStage(GameStage stage)
    {
        Debug.Log("starting stage " + stage);
        mCurrentStage = stage;
        
        if(stage == GameStage.SEARCH_1 || stage == GameStage.SEARCH_2 || stage == GameStage.SEARCH_3)
        {
            // assign npcs to rooms (for now, ensure they go to different rooms)
            int[] roomChoices = Utilities.RandomList(clueRooms.Length, 2);
            for(int i = 0, j = 0; i < 3; ++i)
            {
                if(i != PlayerId)
                {
                    string npcRoom = clueRooms[roomChoices[j++]];
                    MoveToRoom(i, npcRoom);

                    // npcs pick up a clue in the room they move to
                    if(mCluesInRooms.ContainsKey(npcRoom))
                    {
                        List<ClueInfo> cluesInRoom = mCluesInRooms[npcRoom];
                        if(cluesInRoom.Count > 0)
                        {
                            int clueIdx = Random.Range(0, cluesInRoom.Count);
                            ClueInfo info = cluesInRoom[clueIdx];
                            cluesInRoom.RemoveAt(clueIdx);
                            
                            mPeople[i].knowledge.AddKnowledge(info.GetSentence());
                            mRoundClues[i] = info;
                        }
                    }
                }
            }

            MoveToRoom(PlayerId, mCurrentRoom); // hack to reload room with npcs gone
        }
        else if(stage == GameStage.COMMUNAL_1 || stage == GameStage.COMMUNAL_2 || stage == GameStage.COMMUNAL_3)
        {
            for (int i = 0; i < 3; ++i)
            {
                MoveToRoom(i, openingScene);
            }
        }
        else if(stage == GameStage.POLICE)
        {
            MoveToRoom(PlayerId, "EndScene");
        }
    }

    public void MoveToRoom(int personId, string scene)
    {
        mPersonRooms[personId] = scene; // maybe unnecessary, idk

        if(personId == PlayerId)
        {
            if (mPendingRoom != null) { return; }

            mPendingRoom = scene;
            blackFade.SetTrigger("FadeOut");
        }
    }

    public void OnFadeOutComplete()
    {
        if (mCurrentRoom != null)
        {
            mLoadSceneOperation = SceneManager.UnloadSceneAsync(mCurrentRoom);
            mLoadState = LoadState.UNLOADING_SCENE;
            return;
        }

        LoadPendingRoom();
    }

    private void LoadPendingRoom()
    {
        mLoadState = LoadState.LOADING_SCENE;

        mCurrentRoom = mPendingRoom; // do this now, because the objects in the room Start during the load (i.e. before we get to OnRoomLoaded)

        // if we want to keep the game rules object around (and UI too?) then we load scenes additively
        mLoadSceneOperation = SceneManager.LoadSceneAsync(mPendingRoom, LoadSceneMode.Additive);
    }

    public void OnRoomLoaded()
    {
        blackFade.ResetTrigger("FadeOut");
        blackFade.SetTrigger("FadeIn");
        
        mLoadSceneOperation = null;
        mLoadState = LoadState.NONE;
        mPendingRoom = null;

        SceneManager.SetActiveScene(SceneManager.GetSceneByName(mCurrentRoom)); // ensures instantiate objects are added to the current room's scene (so they'll be destroyed when leaving)

        // populate room with clues
        if (mCluesInRooms.ContainsKey(mCurrentRoom))
        {
            // Debug.Log("You find these clues in the room:");
            int clueX = -2;
            foreach (ClueInfo c in mCluesInRooms[mCurrentRoom])
            {
                // Debug.Log(c.mConceptA + " <-> " + c.mConceptB);

                // TODO: markup the scene with valid spawn points for clues (maybe further filtered by type of clue)
                // for now, spawn them wherever
                GameObject clueObj = GameObject.Instantiate(cluePrefab);
                clueObj.GetComponent<Clue>().mInfo = c;
                Vector3 pos = new Vector3(clueX, 0, 0);
                clueObj.transform.position = pos;
                clueX += 1;
            }
        }

        // maybe this is cleaner in its own function, like ContinueGameStage or whatever, idk
        Debug.Log(mCurrentRoom + " loaded. Current stage: " + mCurrentStage);
        if (mCurrentStage == GameStage.MENU)
        {
            mCurrentStage = GameStage.INTRO;

            Debug.Log("Dead body. The name " + mStartingClue.mConceptB + " is written in blood by the body.");
            DialogBlock discussion = new DialogBlock(mPeople, OnDialogueDismissed);
            discussion.QueueDialogue(mPeople[2], new Sprite[] { mPeople[2].HeadSprite }, "What Happened?");
            discussion.QueueDialogue(mPeople[1], new Sprite[] { mPeople[1].HeadSprite }, "Where am I?");
            discussion.QueueDialogue(Player, NonPlayersHeads, "Who am I?");
            discussion.QueueDialogue(mPeople[2], new Sprite[] { SpriteManager.GetSprite("Victim") }, "Look! A body!");
            discussion.QueueDialogue(mPeople[1], new Sprite[] { SpriteManager.GetSprite("CrimeScene") }, "And a name: " + mStartingClue.mConceptB);
            discussion.QueueDialogue(Player, NonPlayersHeads, "Let's split up and look for clues.");
            discussion.Start();
        }
        else if (mCurrentStage == GameStage.COMMUNAL_1 || mCurrentStage == GameStage.COMMUNAL_2)
        {
            DialogBlock discussion = new DialogBlock(mPeople, OnDialogueDismissed);
            discussion.QueueDialogue(mPeople[1], NonPlayersHeads, "What did everyone find?");
            discussion.QueueInformationExchange();
            discussion.QueueDialogue(Player, NonPlayersHeads, "There must be more clues around.");
            discussion.Start();
        }
        else if (mCurrentStage == GameStage.COMMUNAL_3)
        {
            DialogBlock discussion = new DialogBlock(mPeople, OnDialogueDismissed);
            discussion.QueueDialogue(mPeople[1], NonPlayersHeads, "The police are almost here. Lets do a final round of information exchange.");
            discussion.QueueInformationExchange();
            discussion.QueueDialogue(mPeople[2], NonPlayersHeads, "Well, the police are here now.");
            discussion.Start();
        }
        else if(mCurrentStage == GameStage.POLICE)
        {
            DialogBlock discussion = new DialogBlock(mPeople, OnDialogueDismissed);
            discussion.QueueDialogue(null, new Sprite[] { }, "What happened? Which one of you killed the guy?");
            // TODO: ask the player for their opinion, either first or last

            // get npc evaluations
            Sentence killer0 = new Sentence(Noun.Blonde, Verb.Is, Noun.Killer, Adverb.True);
            Sentence killer1 = new Sentence(Noun.Brown, Verb.Is, Noun.Killer, Adverb.True);
            Sentence killer2 = new Sentence(Noun.Red, Verb.Is, Noun.Killer, Adverb.True);
            for (int i = 0; i < 3; ++i)
            {
                if(i != PlayerId)
                {
                    Knowledge personKnowledge = mPeople[i].knowledge;
                    Noun myHair = mPeople[i].AttributeMap[NounType.HairColor];
                    Sprite[] sprite = { mPeople[i].HeadSprite };

                    float confidence0 = personKnowledge.VerifyBelief(killer0);
                    float confidence1 = personKnowledge.VerifyBelief(killer1);
                    float confidence2 = personKnowledge.VerifyBelief(killer2);

                    if (confidence0 > 0 && myHair != Noun.Blonde)
                        discussion.QueueDialogue(mPeople[i], sprite, "I think BLONDE did it (confidence " + confidence0 + ")");
                    else if (confidence1 > 0 && myHair != Noun.Brown)
                        discussion.QueueDialogue(mPeople[i], sprite, "I think BROWN did it (confidence " + confidence1 + ")");
                    else if (confidence2 > 0 && myHair != Noun.Red)
                        discussion.QueueDialogue(mPeople[i], sprite, "I think RED did it (confidence " + confidence2 + ")");
                    else
                    {
                        float innocenceBlonde = personKnowledge.VerifyBelief(new Sentence(Noun.Blonde, Verb.Is, Noun.Killer, Adverb.False));
                        float innocenceBrown = personKnowledge.VerifyBelief(new Sentence(Noun.Brown, Verb.Is, Noun.Killer, Adverb.False));
                        float innocenceRed = personKnowledge.VerifyBelief(new Sentence(Noun.Red, Verb.Is, Noun.Killer, Adverb.False));

                        if (myHair == Noun.Blonde)
                        {
                            if (innocenceBrown > 0f || innocenceRed > 0f)
                            {
                                string innocentName = innocenceBrown > innocenceRed ? "BROWN" : "RED";
                                string guiltyName = innocenceBrown > innocenceRed ? "RED" : "BROWN";
                                discussion.QueueDialogue(mPeople[i], sprite, "Well I didn't do it, and " + innocentName + " didn't do it, so " + guiltyName + " did.");
                            }
                            else
                            {
                                discussion.QueueDialogue(mPeople[i], sprite, "I have no idea.");
                            }
                        }
                        else if (myHair == Noun.Brown)
                        {
                            if (innocenceBlonde > 0f || innocenceRed > 0f)
                            {
                                string innocentName = innocenceBlonde > innocenceRed ? "BLONDE" : "RED";
                                string guiltyName = innocenceBlonde > innocenceRed ? "RED" : "BLONDE";
                                discussion.QueueDialogue(mPeople[i], sprite, "Well I didn't do it, and " + innocentName + " didn't do it, so " + guiltyName + " did.");
                            }
                            else
                            {
                                discussion.QueueDialogue(mPeople[i], sprite, "I have no idea.");
                            }

                        }
                        else if (myHair == Noun.Red)
                        {
                            if (innocenceBrown > 0f || innocenceBlonde > 0f)
                            {
                                string innocentName = innocenceBrown > innocenceBlonde ? "BROWN" : "BLONDE";
                                string guiltyName = innocenceBrown > innocenceBlonde ? "BLONDE" : "BROWN";
                                discussion.QueueDialogue(mPeople[i], sprite, "Well I didn't do it, and " + innocentName + " didn't do it, so " + guiltyName + " did.");
                            }
                            else
                            {
                                discussion.QueueDialogue(mPeople[i], sprite, "I have no idea.");
                            }
                        }
                    }
                }
            }
            discussion.Start();
        }
    }

    public PersonState GetPerson(int personId)
    {
        return mPeople[personId];
    }

    public bool IsPersonInCurrentRoom(int personId)
    {
        // check if we should display the given person.
        // special case is that because we're first person,
        // we never show ourselves (maybe we don't even need to update the player's location?)
        
        return (personId != PlayerId && mPersonRooms[personId].Equals(mCurrentRoom));
    }


    private void GetRoomChoice(PersonObject p)
    {
        
    }
}
