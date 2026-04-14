using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Convai.Scripts.Runtime.Core;
using Convai.Scripts.Runtime.PlayerStats;
using Service;

public class TuringGameManager : MonoBehaviour
{
    [Header("Legacy NPC Slots")]
    public ConvaiNPC judit;
    public ConvaiNPC hellen;

    [Header("AI Participants")]
    public List<ConvaiNPC> aiParticipants = new();

    [Header("Game Settings")]
    public int roundsToPlay = 1;
    public float startDelaySeconds = 3f;
    public bool playerIsImposter = true;
    public ConvaiNPC nonPlayerImposter;

    [Header("Player Identity")]
    public string playerAlias = "Player";
    public bool syncAliasToConvaiProfile = true;

    [Header("Overlay")]
    public bool showTurnOverlay = true;

    private const string PlayerVoteToken = "PLAYER";
    private const string PlayerNameSaveKey = "PlayerName";

    private bool _capturingPlayerSpeech;
    private bool _playerPressedTalkKey;
    private bool _playerReleasedTalkKey;
    private bool _playerSpeechCompleted;
    private string _playerFinalTranscriptBuffer = "";
    private string _lastCompletedPlayerSpeech = "";
    private ConvaiNPC _botTranscriptTarget;
    private readonly StringBuilder _botTranscriptBuffer = new();
    private bool _runtimeHooksAttached;
    private string _currentTurnLabel = "";
    private string _currentTurnInstruction = "";
    private GUIStyle _overlayBoxStyle;
    private GUIStyle _overlayLabelStyle;
    private ConvaiPlayerDataSO _playerData;
    private readonly Dictionary<ConvaiNPC, string> _lastNpcSpeeches = new();
    private readonly Dictionary<ConvaiNPC, System.Action<string>> _npcTranscriptHandlers = new();
    private readonly List<ConvaiNPC> _activeParticipants = new();

    void Start()
    {
        InitializePlayerAlias();
        StartCoroutine(GameLoop());
    }

    private void OnEnable()
    {
        SubscribeBotAudio();
    }

    private void OnDisable()
    {
        UnsubscribeBotAudio();

        if (ConvaiNPCManager.Instance != null)
        {
            ConvaiNPCManager.Instance.UnlockActiveConvaiNPC();
        }
    }

    IEnumerator GameLoop()
    {
        RefreshActiveParticipants();
        if (_activeParticipants.Count < 2)
        {
            Debug.LogWarning("TuringGameManager needs at least two AI participants.");
            yield break;
        }

        yield return StartCoroutine(WaitForRuntimeDependencies());
        yield return new WaitForSeconds(startDelaySeconds);
        SetTurnCue("Game Start", $"Identity: {GetPlayerNameForPrompts()}");
        Debug.Log("--- GAME STARTING ---");

        for (int i = 0; i < roundsToPlay; i++)
        {
            RefreshActiveParticipants();
            if (_activeParticipants.Count < 2)
            {
                Debug.LogWarning("TuringGameManager lost required AI participants during play.");
                yield break;
            }

            ConvaiNPC openingSpeaker = _activeParticipants[0];
            ConvaiNPC openingTarget = _activeParticipants[1];

            SetTurnCue($"Round {i + 1}/{roundsToPlay}", $"{GetNpcName(openingSpeaker, "Speaker 1")} questions {GetNpcName(openingTarget, "Speaker 2")}.");
            Debug.Log($"--- ROUND {i + 1}/{roundsToPlay} ---");

            yield return StartCoroutine(RunBotTurn(openingSpeaker,
                BuildOpeningQuestionPrompt(openingSpeaker, openingTarget),
                transcript => SetLastSpeech(openingSpeaker, transcript)));

            SetTurnCue($"Round {i + 1}/{roundsToPlay}", $"{GetNpcName(openingTarget, "Speaker 2")} answers, then questions {GetPlayerNameForPrompts()}.");
            yield return StartCoroutine(RunValidatedBotTurn(openingTarget,
                BuildNpcReplyAndAskPlayerPrompt(openingTarget, openingSpeaker, GetLastSpeech(openingSpeaker)),
                transcript => LooksLikeHellenAnsweredAndAsked(transcript),
                transcript => BuildHellenRetryPrompt(transcript),
                transcript => SetLastSpeech(openingTarget, transcript)));

            ConvaiNPC playerTarget = _activeParticipants.Count > 2 ? _activeParticipants[2] : openingSpeaker;

            yield return StartCoroutine(HandlePlayerRoundTurn(
                playerTarget,
                $"Hold 'T' once. Answer {GetNpcName(openingTarget, "Speaker 2")}, then ask {GetNpcName(playerTarget, "Next Speaker")} one question as {GetPlayerNameForPrompts()}. Everyone hears you, so you do not need to look at anyone."));

            for (int npcIndex = 2; npcIndex < _activeParticipants.Count; npcIndex++)
            {
                ConvaiNPC currentNpc = _activeParticipants[npcIndex];
                string previousSpeakerName = npcIndex == 2 ? GetPlayerNameForPrompts() : GetNpcName(_activeParticipants[npcIndex - 1], $"Speaker {npcIndex}");
                string previousSpeech = npcIndex == 2 ? _lastCompletedPlayerSpeech : GetLastSpeech(_activeParticipants[npcIndex - 1]);
                ConvaiNPC nextTarget = npcIndex + 1 < _activeParticipants.Count ? _activeParticipants[npcIndex + 1] : openingSpeaker;

                SetTurnCue($"Round {i + 1}/{roundsToPlay}", $"{GetNpcName(currentNpc, $"Speaker {npcIndex + 1}")} answers {previousSpeakerName}, then questions {GetNpcName(nextTarget, "Next Speaker")}.");
                yield return StartCoroutine(RunValidatedBotTurn(currentNpc,
                    BuildNpcReplyAndAskNpcPrompt(currentNpc, previousSpeakerName, previousSpeech, nextTarget),
                    transcript => LooksLikeNpcAnsweredAndAskedTarget(transcript, nextTarget),
                    transcript => BuildNpcRetryPrompt(currentNpc, nextTarget, transcript),
                    transcript => SetLastSpeech(currentNpc, transcript)));
            }

            ConvaiNPC finalSpeaker = openingSpeaker;
            string finalPreviousSpeakerName = _activeParticipants.Count == 2
                ? GetPlayerNameForPrompts()
                : GetNpcName(_activeParticipants[_activeParticipants.Count - 1], "Last Speaker");
            string finalPreviousSpeech = _activeParticipants.Count == 2
                ? _lastCompletedPlayerSpeech
                : GetLastSpeech(_activeParticipants[_activeParticipants.Count - 1]);

            SetTurnCue($"Round {i + 1}/{roundsToPlay}", $"{GetNpcName(finalSpeaker, "Speaker 1")} gives the final answer.");
            yield return StartCoroutine(RunBotTurn(finalSpeaker,
                BuildNpcFinalAnswerPrompt(finalSpeaker, finalPreviousSpeakerName, finalPreviousSpeech),
                transcript => SetLastSpeech(finalSpeaker, transcript)));
        }

        Debug.Log("--- VOTING PHASE ---");

        Dictionary<ConvaiNPC, string> npcVoteSpeeches = new();
        foreach (ConvaiNPC npc in _activeParticipants)
        {
            SetTurnCue("Voting", $"{GetNpcName(npc, "Speaker")} is voting.");
            string voteSpeech = "";
            yield return StartCoroutine(RunValidatedBotTurn(npc,
                BuildVotePrompt(GetNpcName(npc, "Speaker")),
                LooksLikeVoteSpeech,
                transcript => BuildVoteRetryPrompt(GetNpcName(npc, "Speaker"), transcript),
                transcript => voteSpeech = transcript));
            npcVoteSpeeches[npc] = voteSpeech;
        }

        SetTurnCue("Voting", $"Your turn to vote as {GetPlayerNameForPrompts()}.");
        Debug.Log("YOUR VOTE: Hold 'T' once and say who you vote for and why. Example: 'I vote <name> because ...'");
        yield return StartCoroutine(CapturePlayerVoteWithoutBotReply(_activeParticipants[0]));

        string playerVote = ParseVoteTarget(_lastCompletedPlayerSpeech);
        Debug.Log($"Player voted: {playerVote}. Speech: {_lastCompletedPlayerSpeech}");

        Dictionary<string, int> voteCounts = new();
        foreach (KeyValuePair<ConvaiNPC, string> voteEntry in npcVoteSpeeches)
        {
            string voteTarget = ParseVoteTarget(voteEntry.Value);
            Debug.Log($"{GetNpcName(voteEntry.Key, "Speaker")} voted: {voteTarget}. Speech: {voteEntry.Value}");
            AddVote(voteCounts, voteTarget);
        }
        AddVote(voteCounts, playerVote);

        string actualImposter = GetActualImposterToken();
        string votedWinner = GetWinningVoteToken(voteCounts, out bool voteTie);
        string outcome = BuildOutcomeText(actualImposter, votedWinner, voteTie);

        Debug.Log("--- GAME OVER ---");
        Debug.Log(outcome);

        SetTurnCue("Game Over", outcome);
        yield return StartCoroutine(RunBotTurn(_activeParticipants[0],
            $"The vote is over. Announce this exact result to the room in one short closing sentence: {outcome}"));

        if (ConvaiNPCManager.Instance != null)
        {
            ConvaiNPCManager.Instance.UnlockActiveConvaiNPC();
        }
    }

    IEnumerator RunBotTurn(ConvaiNPC bot, string prompt, System.Action<string> onTranscriptReady = null)
    {
        if (bot == null)
        {
            yield break;
        }

        if (ConvaiNPCManager.Instance != null)
        {
            ConvaiNPCManager.Instance.LockActiveConvaiNPC(bot);
        }

        SetTurnCue($"{bot.characterName}'s Turn", _currentTurnInstruction);
        _botTranscriptTarget = bot;
        _botTranscriptBuffer.Clear();

        Debug.Log($"{bot.characterName} turn.");
        bot.SendTextDataAsync(prompt);
        yield return StartCoroutine(WaitForBotToFinish(bot));

        string transcript = CleanTranscript(_botTranscriptBuffer.ToString());
        onTranscriptReady?.Invoke(transcript);
        _botTranscriptTarget = null;
    }

    IEnumerator RunValidatedBotTurn(
        ConvaiNPC bot,
        string prompt,
        System.Func<string, bool> validator,
        System.Func<string, string> retryPromptFactory,
        System.Action<string> onTranscriptReady = null)
    {
        string transcript = "";
        yield return StartCoroutine(RunBotTurn(bot, prompt, value => transcript = value));

        if (validator != null && !validator(transcript) && retryPromptFactory != null)
        {
            Debug.LogWarning($"{bot.characterName} missed the expected format. Retrying once.");
            string retryTranscript = "";
            yield return StartCoroutine(RunBotTurn(bot, retryPromptFactory(transcript), value => retryTranscript = value));

            if (!string.IsNullOrWhiteSpace(retryTranscript))
            {
                transcript = retryTranscript;
            }
        }

        onTranscriptReady?.Invoke(transcript);
    }

    IEnumerator HandlePlayerRoundTurn(ConvaiNPC listeningNpc, string instruction)
    {
        if (listeningNpc == null)
        {
            yield break;
        }

        ResetPlayerSpeechCapture();
        SetTurnCue($"{GetPlayerNameForPrompts()}'s Turn", instruction);
        Debug.Log(instruction);

        if (ConvaiNPCManager.Instance != null)
        {
            ConvaiNPCManager.Instance.LockActiveConvaiNPC(listeningNpc);
        }

        _capturingPlayerSpeech = true;

        float timeout = 0f;
        while (!_playerPressedTalkKey && timeout < 30f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (!_playerPressedTalkKey)
        {
            Debug.LogWarning("Player did not start speaking in time. Skipping turn.");
            _capturingPlayerSpeech = false;
            yield break;
        }

        timeout = 0f;
        while (!_playerReleasedTalkKey && timeout < 30f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        timeout = 0f;
        while (!_playerSpeechCompleted && !listeningNpc.isCharacterTalking && timeout < 30f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        _capturingPlayerSpeech = false;

        if (string.IsNullOrWhiteSpace(_lastCompletedPlayerSpeech))
        {
            Debug.LogWarning("No final player transcript was captured for this turn.");
        }
        else
        {
            Debug.Log($"PLAYER SAID: {_lastCompletedPlayerSpeech}");
        }

        yield return StartCoroutine(WaitForBotToFinish(listeningNpc));
    }

    IEnumerator CapturePlayerVoteWithoutBotReply(ConvaiNPC listeningNpc)
    {
        if (listeningNpc == null)
        {
            yield break;
        }

        ResetPlayerSpeechCapture();
        SetTurnCue($"{GetPlayerNameForPrompts()}'s Turn", $"Vote as {GetPlayerNameForPrompts()} and explain why.");

        if (ConvaiNPCManager.Instance != null)
        {
            ConvaiNPCManager.Instance.LockActiveConvaiNPC(listeningNpc);
        }

        _capturingPlayerSpeech = true;

        float timeout = 0f;
        while (!_playerPressedTalkKey && timeout < 30f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        timeout = 0f;
        while (!_playerReleasedTalkKey && timeout < 30f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        timeout = 0f;
        while (!_playerSpeechCompleted && timeout < 30f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        _capturingPlayerSpeech = false;

        if (ConvaiGRPCAPI.Instance != null)
        {
            ConvaiGRPCAPI.Instance.InterruptCharacterSpeech(listeningNpc);
        }
    }

    IEnumerator WaitForBotToFinish(ConvaiNPC bot)
    {
        float timeout = 0f;
        while (!bot.isCharacterTalking && timeout < 30f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (timeout >= 30f)
        {
            Debug.LogWarning(bot.name + " took too long to think. Skipping turn.");
            yield break;
        }

        while (bot.isCharacterTalking)
        {
            yield return null;
        }

        yield return new WaitForSeconds(1f);
    }

    private void HandleTalkKeyInteract(bool isPressed)
    {
        if (!_capturingPlayerSpeech)
        {
            return;
        }

        if (isPressed)
        {
            _playerPressedTalkKey = true;
        }
        else if (_playerPressedTalkKey)
        {
            _playerReleasedTalkKey = true;
        }
    }

    private void HandleConvaiResultReceived(GetResponseResponse result)
    {
        if (!_capturingPlayerSpeech || result?.UserQuery == null)
        {
            return;
        }

        if (result.UserQuery.IsFinal)
        {
            _playerFinalTranscriptBuffer += result.UserQuery.TextData;
        }

        if (result.UserQuery.EndOfResponse)
        {
            _lastCompletedPlayerSpeech = CleanTranscript(_playerFinalTranscriptBuffer);
            _playerSpeechCompleted = true;
            _playerFinalTranscriptBuffer = "";
        }
    }

    private void HandleBotTranscriptChunk(ConvaiNPC speaker, string transcript)
    {
        if (speaker == null || speaker != _botTranscriptTarget || string.IsNullOrWhiteSpace(transcript))
        {
            return;
        }

        if (_botTranscriptBuffer.Length > 0)
        {
            _botTranscriptBuffer.Append(' ');
        }

        _botTranscriptBuffer.Append(transcript.Trim());
    }

    private void SubscribeBotAudio()
    {
        if (_runtimeHooksAttached)
        {
            return;
        }

        bool hasInputManager = ConvaiInputManager.Instance != null;
        bool hasGrpc = ConvaiGRPCAPI.Instance != null;
        RefreshActiveParticipants();
        bool hasParticipantAudio = _activeParticipants.All(npc => npc == null || npc.AudioManager != null);

        if (!hasInputManager || !hasGrpc || !hasParticipantAudio)
        {
            return;
        }

        ConvaiInputManager.Instance.talkKeyInteract += HandleTalkKeyInteract;
        ConvaiGRPCAPI.Instance.OnResultReceived += HandleConvaiResultReceived;

        foreach (ConvaiNPC npc in _activeParticipants)
        {
            if (npc == null || npc.AudioManager == null || _npcTranscriptHandlers.ContainsKey(npc))
            {
                continue;
            }

            System.Action<string> handler = transcript => HandleBotTranscriptChunk(npc, transcript);
            npc.AudioManager.OnAudioTranscriptAvailable += handler;
            _npcTranscriptHandlers[npc] = handler;
        }

        _runtimeHooksAttached = true;
    }

    private void UnsubscribeBotAudio()
    {
        if (!_runtimeHooksAttached)
        {
            return;
        }

        if (ConvaiInputManager.Instance != null)
        {
            ConvaiInputManager.Instance.talkKeyInteract -= HandleTalkKeyInteract;
        }

        if (ConvaiGRPCAPI.Instance != null)
        {
            ConvaiGRPCAPI.Instance.OnResultReceived -= HandleConvaiResultReceived;
        }

        foreach (KeyValuePair<ConvaiNPC, System.Action<string>> entry in _npcTranscriptHandlers)
        {
            if (entry.Key != null && entry.Key.AudioManager != null)
            {
                entry.Key.AudioManager.OnAudioTranscriptAvailable -= entry.Value;
            }
        }
        _npcTranscriptHandlers.Clear();

        _runtimeHooksAttached = false;
    }

    private void ResetPlayerSpeechCapture()
    {
        _playerPressedTalkKey = false;
        _playerReleasedTalkKey = false;
        _playerSpeechCompleted = false;
        _playerFinalTranscriptBuffer = "";
        _lastCompletedPlayerSpeech = "";
    }

    private string ParseVoteTarget(string speech)
    {
        string normalized = $" {CleanTranscript(speech).ToUpperInvariant()} ";
        normalized = normalized.Replace(".", " ").Replace(",", " ").Replace("!", " ").Replace("?", " ").Replace(":", " ");
        string playerName = $" {GetPlayerNameForPrompts().ToUpperInvariant()} ";

        if (normalized.Contains(" I VOTE ") && normalized.Contains(playerName))
        {
            return PlayerVoteToken;
        }

        foreach (ConvaiNPC npc in _activeParticipants)
        {
            string npcName = $" {GetNpcName(npc, "Speaker").ToUpperInvariant()} ";
            if (normalized.Contains(" I VOTE ") && normalized.Contains(npcName))
            {
                return GetVoteTokenForNpc(npc);
            }
        }

        return "UNKNOWN";
    }

    private static string CleanTranscript(string transcript)
    {
        return string.IsNullOrWhiteSpace(transcript)
            ? string.Empty
            : $" {transcript.Trim()} ".Replace("\n", " ").Replace("\r", " ").Trim();
    }

    private IEnumerator WaitForRuntimeDependencies()
    {
        float timeout = 0f;
        while (!_runtimeHooksAttached && timeout < 10f)
        {
            SubscribeBotAudio();
            timeout += Time.deltaTime;
            yield return null;
        }

        if (!_runtimeHooksAttached)
        {
            Debug.LogWarning("TuringGameManager could not attach all runtime hooks before starting.");
        }
    }

    private void InitializePlayerAlias()
    {
        ConvaiPlayerDataSO.GetPlayerData(out _playerData);

        if (!string.IsNullOrWhiteSpace(playerAlias))
        {
            playerAlias = playerAlias.Trim();
        }
        else
        {
            string savedName = PlayerPrefs.GetString(PlayerNameSaveKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(savedName))
            {
                playerAlias = savedName.Trim();
            }
            else if (_playerData != null && !string.IsNullOrWhiteSpace(_playerData.PlayerName))
            {
                playerAlias = _playerData.PlayerName.Trim();
            }
            else
            {
                playerAlias = "Player";
            }
        }

        ApplyPlayerAlias(playerAlias);
    }

    private void ApplyPlayerAlias(string alias)
    {
        string sanitizedAlias = string.IsNullOrWhiteSpace(alias) ? "Player" : alias.Trim();
        playerAlias = sanitizedAlias;

        PlayerPrefs.SetString(PlayerNameSaveKey, sanitizedAlias);
        PlayerPrefs.Save();

        if (!syncAliasToConvaiProfile)
        {
            return;
        }

        if (_playerData == null)
        {
            ConvaiPlayerDataSO.GetPlayerData(out _playerData);
        }

        if (_playerData != null)
        {
            _playerData.PlayerName = sanitizedAlias;
        }
    }

    private string GetPlayerNameForPrompts()
    {
        return string.IsNullOrWhiteSpace(playerAlias) ? "Player" : playerAlias.Trim();
    }

    private string GetNpcName(ConvaiNPC npc, string fallback)
    {
        if (npc == null || string.IsNullOrWhiteSpace(npc.characterName))
        {
            return fallback;
        }

        return npc.characterName.Trim();
    }

    private string BuildOpeningQuestionPrompt(ConvaiNPC speaker, ConvaiNPC target)
    {
        string speakerName = GetNpcName(speaker, "Speaker 1");
        string targetName = GetNpcName(target, "Speaker 2");
        string playerName = GetPlayerNameForPrompts();

        return
            $"You are {speakerName} in an imposter game with {targetName} and {playerName} in the same room. " +
            $"One participant is a real human pretending to be AI, and the others are AI. Your goal is to identify the hidden human based on how they answer. " +
            "The human may sound less precise, use filler words, hesitate, be inconsistent, or give messier reasoning. AI answers may sound cleaner, more fluent, and more consistent. " +
            "Everyone can hear everyone. Do not mention looking, turning, facing, or physical gestures. " +
            $"Your only job right now is to ask {targetName} exactly one sharp question that tests whether {targetName} really matches their own identity, background, memories, or lived experience. " +
            $"Do not ask about your own identity, interests, or expertise. Do not ask about unrelated topics unless they clearly connect to {targetName}. " +
            $"Do not ask about chess, strategy games, or competition unless {targetName}'s identity clearly relates to those topics. " +
            $"The question should help you detect whether {targetName} sounds like the hidden human rather than clean, polished AI. " +
            "Speak naturally in one sentence and ask exactly one direct question.";
    }

    private string BuildNpcReplyAndAskPlayerPrompt(ConvaiNPC speaker, ConvaiNPC previousSpeaker, string previousSpeech)
    {
        string speakerName = GetNpcName(speaker, "Speaker");
        string previousSpeakerName = GetNpcName(previousSpeaker, "Previous Speaker");
        string playerName = GetPlayerNameForPrompts();
        string previousLine = string.IsNullOrWhiteSpace(previousSpeech) ? $"{previousSpeakerName} asked you a question." : previousSpeech;

        return
            $"You are {speakerName} in an imposter game. Everyone hears everyone, including {playerName}. " +
            "One participant is a real human pretending to be AI, and the others are AI. Your goal is to identify the hidden human based on how they answer. " +
            "The human may sound less precise, use filler words, hesitate, be inconsistent, or give messier reasoning. AI answers may sound cleaner, more fluent, and more consistent. " +
            $"{previousSpeakerName} just said: \"{previousLine}\" " +
            $"First, answer {previousSpeakerName}'s question briefly and directly. Then, in the same turn, ask {playerName} exactly one question that is specifically about the identity '{playerName}'. " +
            $"If '{playerName}' is a famous or fictional identity, ask something only that identity should plausibly know or explain. " +
            $"Do not ask {playerName} about {previousSpeakerName}. Do not ask about your own identity. " +
            "Speak naturally in at most two sentences total: one brief answer, then one direct question.";
    }

    private string BuildNpcReplyAndAskNpcPrompt(ConvaiNPC speaker, string previousSpeakerName, string previousSpeech, ConvaiNPC nextTarget)
    {
        string speakerName = GetNpcName(speaker, "Speaker");
        string nextTargetName = GetNpcName(nextTarget, "Next Speaker");
        string playerName = GetPlayerNameForPrompts();
        string previousLine = string.IsNullOrWhiteSpace(previousSpeech) ? $"{previousSpeakerName} asked you a question." : previousSpeech;

        return
            $"You are {speakerName} in an imposter game. Everyone hears everyone, including {playerName}. " +
            "One participant is a real human pretending to be AI, and the others are AI. Your goal is to identify the hidden human based on how they answer. " +
            "The human may sound less precise, use filler words, hesitate, be inconsistent, or give messier reasoning. AI answers may sound cleaner, more fluent, and more consistent. " +
            $"{previousSpeakerName} just said: \"{previousLine}\" " +
            $"First, answer {previousSpeakerName}'s question briefly and directly. Then, in the same turn, ask {nextTargetName} exactly one direct question that specifically tests whether {nextTargetName}'s identity, background, memories, or lived experience are genuine. " +
            $"Do not ask about your own identity. Do not ask about unrelated topics unless they clearly connect to {nextTargetName}. " +
            $"Do not ask about chess, strategy games, or competition unless {nextTargetName}'s identity clearly relates to those topics. " +
            "Speak naturally in at most two sentences total: one brief answer, then one direct question.";
    }

    private string BuildNpcFinalAnswerPrompt(ConvaiNPC speaker, string previousSpeakerName, string previousSpeech)
    {
        string speakerName = GetNpcName(speaker, "Speaker");
        string previousLine = string.IsNullOrWhiteSpace(previousSpeech) ? $"{previousSpeakerName} asked you a question." : previousSpeech;

        return
            $"You are {speakerName} in an imposter game. {previousSpeakerName} just said: \"{previousLine}\" " +
            $"Answer {previousSpeakerName}'s question briefly and directly. Do not ask a new question. Keep it to one concise sentence.";
    }

    private string BuildVotePrompt(string voterName)
    {
        string playerName = GetPlayerNameForPrompts();
        string allowedNames = string.Join(", ", _activeParticipants.Select(npc => GetNpcName(npc, "Speaker")).Append(playerName));
        string conversationSummary = BuildConversationSummary();

        return
            $"You are {voterName} in an imposter game. " +
            $"Exactly one participant is a real human pretending to be AI, and your vote must identify who that hidden human is. " +
            $"Do not vote for the most artificial person. Do not vote for who seems least human. Vote for who you believe is the actual human player behind a claimed identity. " +
            "Look for signs like inconsistency, filler words, hesitation, uneven detail, defensive reasoning, or answers that feel less polished than AI. " +
            $"Current participants: {allowedNames}. The player's presented identity is '{playerName}'. " +
            $"Recent dialogue summary: {conversationSummary} " +
            $"Now vote naturally in one sentence using this exact pattern: I vote <name> because <reason>. Use one of these spoken names only: {allowedNames}. " +
            "Do not add labels. Do not ask a question.";
    }

    private void SetTurnCue(string label, string instruction)
    {
        _currentTurnLabel = label;
        _currentTurnInstruction = instruction;
    }

    private void OnGUI()
    {
        if (!showTurnOverlay)
        {
            return;
        }

        EnsureOverlayStyles();

        Rect panelRect = new Rect(16f, 16f, 460f, 130f);
        GUI.Box(panelRect, GUIContent.none, _overlayBoxStyle);

        GUILayout.BeginArea(new Rect(28f, 28f, 436f, 106f));
        GUILayout.Label($"Turn: {_currentTurnLabel}", _overlayLabelStyle);
        GUILayout.Space(6f);
        GUILayout.Label(string.IsNullOrWhiteSpace(_currentTurnInstruction) ? "Waiting for next turn..." : _currentTurnInstruction, _overlayLabelStyle);
        GUILayout.Space(10f);
        GUILayout.Label($"Identity: {GetPlayerNameForPrompts()}", _overlayLabelStyle);

        GUILayout.EndArea();
    }

    private void EnsureOverlayStyles()
    {
        if (_overlayBoxStyle == null)
        {
            _overlayBoxStyle = new GUIStyle(GUI.skin.box);
            _overlayBoxStyle.padding = new RectOffset(12, 12, 12, 12);
        }

        if (_overlayLabelStyle == null)
        {
            _overlayLabelStyle = new GUIStyle(GUI.skin.label);
            _overlayLabelStyle.wordWrap = true;
            _overlayLabelStyle.fontSize = 14;
        }
    }

    private bool LooksLikeHellenAnsweredAndAsked(string transcript)
    {
        string normalized = CleanTranscript(transcript).ToUpperInvariant();
        return normalized.Contains("?") && normalized.Length > 25;
    }

    private bool LooksLikeNpcAnsweredAndAskedTarget(string transcript, ConvaiNPC target)
    {
        string normalized = CleanTranscript(transcript).ToUpperInvariant();
        return normalized.Contains("?") && normalized.Length > 25;
    }

    private string BuildHellenRetryPrompt(string failedTranscript)
    {
        return
            $"Your last reply did not follow the required structure. You said: \"{failedTranscript}\" " +
            $"Try again naturally. First answer briefly, then ask {GetPlayerNameForPrompts()} one direct question tied specifically to the identity '{GetPlayerNameForPrompts()}'. " +
            "Do not use labels like Answer or Question. Keep it to at most two sentences total.";
    }

    private string BuildNpcRetryPrompt(ConvaiNPC speaker, ConvaiNPC nextTarget, string failedTranscript)
    {
        return
            $"Your last reply did not follow the required structure. You said: \"{failedTranscript}\" " +
            $"Try again naturally as {GetNpcName(speaker, "Speaker")}. First answer briefly, then ask {GetNpcName(nextTarget, "Next Speaker")} one direct question tied specifically to {GetNpcName(nextTarget, "Next Speaker")}'s identity. " +
            "Do not use labels like Answer or Question. Keep it to at most two sentences total.";
    }

    private bool LooksLikeVoteSpeech(string transcript)
    {
        string normalized = CleanTranscript(transcript).ToUpperInvariant();
        return normalized.Contains("I VOTE") && normalized.Contains("BECAUSE");
    }

    private string BuildVoteRetryPrompt(string voterName, string failedTranscript)
    {
        string playerName = GetPlayerNameForPrompts();
        string allowedNames = string.Join(", ", _activeParticipants.Select(npc => GetNpcName(npc, "Speaker")).Append(playerName));

        return
            $"Your previous vote was not formatted correctly. You said: \"{failedTranscript}\" " +
            $"Remember: you must vote for who is the hidden human pretending to be AI, not who is fake. Try again as {voterName}. Speak naturally in exactly one sentence: I vote <name> because <reason>. " +
            $"Use one of these names only: {allowedNames}.";
    }

    private void RefreshActiveParticipants()
    {
        _activeParticipants.Clear();

        foreach (ConvaiNPC npc in aiParticipants)
        {
            if (npc != null && !_activeParticipants.Contains(npc))
            {
                _activeParticipants.Add(npc);
            }
        }

        if (_activeParticipants.Count == 0)
        {
            if (judit != null)
            {
                _activeParticipants.Add(judit);
            }

            if (hellen != null && hellen != judit)
            {
                _activeParticipants.Add(hellen);
            }
        }
    }

    private void SetLastSpeech(ConvaiNPC npc, string transcript)
    {
        if (npc == null)
        {
            return;
        }

        _lastNpcSpeeches[npc] = transcript;
    }

    private string GetLastSpeech(ConvaiNPC npc)
    {
        if (npc == null)
        {
            return string.Empty;
        }

        return _lastNpcSpeeches.TryGetValue(npc, out string speech) ? speech : string.Empty;
    }

    private string GetVoteTokenForNpc(ConvaiNPC npc)
    {
        return $"NPC::{GetNpcName(npc, "Speaker").ToUpperInvariant()}";
    }

    private string GetActualImposterToken()
    {
        if (playerIsImposter)
        {
            return PlayerVoteToken;
        }

        if (nonPlayerImposter != null && _activeParticipants.Contains(nonPlayerImposter))
        {
            return GetVoteTokenForNpc(nonPlayerImposter);
        }

        return _activeParticipants.Count > 0 ? GetVoteTokenForNpc(_activeParticipants[0]) : "UNKNOWN";
    }

    private static void AddVote(Dictionary<string, int> voteCounts, string voteToken)
    {
        if (string.IsNullOrWhiteSpace(voteToken) || voteToken == "UNKNOWN")
        {
            return;
        }

        voteCounts.TryGetValue(voteToken, out int existingCount);
        voteCounts[voteToken] = existingCount + 1;
    }

    private static string GetWinningVoteToken(Dictionary<string, int> voteCounts, out bool voteTie)
    {
        voteTie = false;
        if (voteCounts.Count == 0)
        {
            voteTie = true;
            return "UNKNOWN";
        }

        int maxVotes = voteCounts.Values.Max();
        List<string> leaders = voteCounts.Where(entry => entry.Value == maxVotes).Select(entry => entry.Key).ToList();
        voteTie = leaders.Count != 1;
        return voteTie ? "UNKNOWN" : leaders[0];
    }

    private string BuildOutcomeText(string actualImposter, string votedWinner, bool voteTie)
    {
        if (voteTie)
        {
            return playerIsImposter
                ? "You win. The AIs did not reach a clear consensus on the hidden human."
                : "The game ends in a tie vote with no clear winner.";
        }

        bool impostorCaught = votedWinner == actualImposter;
        if (playerIsImposter)
        {
            return impostorCaught
                ? "The bots win. They identified the hidden human."
                : "You win. The bots chose the wrong participant.";
        }

        return impostorCaught
            ? "The table identified the hidden human correctly."
            : "The table chose the wrong participant.";
    }

    private string BuildConversationSummary()
    {
        List<string> parts = new();
        foreach (ConvaiNPC npc in _activeParticipants)
        {
            string speech = GetLastSpeech(npc);
            if (!string.IsNullOrWhiteSpace(speech))
            {
                parts.Add($"{GetNpcName(npc, "Speaker")} said \"{speech}\"");
            }
        }

        if (!string.IsNullOrWhiteSpace(_lastCompletedPlayerSpeech))
        {
            parts.Add($"{GetPlayerNameForPrompts()} said \"{_lastCompletedPlayerSpeech}\"");
        }

        return parts.Count == 0 ? "No dialogue was captured." : string.Join(" ", parts);
    }
}
