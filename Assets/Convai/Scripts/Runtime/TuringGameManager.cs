using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convai.Scripts.Runtime.Core; 

public class TuringGameManager : MonoBehaviour
{
    [Header("Participants")]
    public List<ConvaiNPC> bots; 
    
    [Header("Game State")]
    public int totalRounds = 2;
    private int currentRound = 1;
    private int currentBotIndex = 0;
    private bool isPlayerTurn = false;
    private bool votingPhase = false;

    void Start()
    {
        Invoke("BeginTest", 3f);
    }

    void BeginTest()
    {
        Debug.Log("Test Beginning...");
        TriggerBotTurn("The Turing test has begun. Introduce yourself and ask a suspicious question to see who is human.");
    }

    public void TriggerBotTurn(string prompt)
    {
        if (votingPhase || bots.Count == 0) return;

        ConvaiNPC currentBot = bots[currentBotIndex];
        Debug.Log($"It is {currentBot.name}'s turn.");
        
        currentBot.SendTextDataAsync(prompt);
        
        // Auto-Watch the bot so we know when to pass the turn
        StartCoroutine(WatchBotTalking(currentBot));
    }

    // THIS IS THE MAGIC WATCHER
    IEnumerator WatchBotTalking(ConvaiNPC bot)
    {
        // 1. Wait until the bot actually STARTS talking (the checkbox turns ON)
        float timeout = 0;
        while (!bot.isCharacterTalking && timeout < 5f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        // 2. Now, wait until the bot STOPS talking (the checkbox turns OFF)
        yield return new WaitUntil(() => !bot.isCharacterTalking);

        Debug.Log($"{bot.name} has finished speaking.");

        // 3. Move to the next bot
        currentBotIndex++;

        if (currentBotIndex >= bots.Count)
        {
            PreparePlayerTurn();
        }
        else
        {
            // Trigger the next bot with a handoff prompt!
            TriggerBotTurn("The previous person just spoke. Respond to them or ask the next person a question.");
        }
    }

    void PreparePlayerTurn()
    {
        isPlayerTurn = true;
        Debug.Log("Player, it is your turn! Speak into your microphone.");
        
        // For testing right now, we will simulate the player's turn ending after 10 seconds.
        // Later, we can hook this up to your VR mic.
        StartCoroutine(SimulatePlayerTurn());
    }

    IEnumerator SimulatePlayerTurn()
    {
        yield return new WaitForSeconds(10f); 
        OnPlayerFinishedSpeaking("I am totally a normal AI, I promise.");
    }

    public void OnPlayerFinishedSpeaking(string playerTranscript)
    {
        if (!isPlayerTurn) return;

        isPlayerTurn = false;
        currentBotIndex = 0;
        currentRound++;

        if (currentRound > totalRounds)
        {
            StartVoting();
        }
        else
        {
            TriggerBotTurn($"The human just said: '{playerTranscript}'. Analyze that and continue questioning.");
        }
    }

    void StartVoting()
    {
        votingPhase = true;
        Debug.Log("VOTING PHASE BEGUN.");
        foreach (var bot in bots)
        {
            bot.SendTextDataAsync("The test is over. Point out who you think is the human.");
        }
    }
}