using System;
using System.Collections.Generic;
using System.Threading;
using Elara.Core;
using Elara.Pipeline;

namespace Elara.Pipeline.UnitTests;

public class ConversationStateMachineTests
{
    [Fact]
    public void Quiescent_To_Listening_On_WakeWord()
    {
        var log = new TestLog();
        var t0 = DateTimeOffset.UtcNow;
        var time = new ManualTimeProvider(t0);
        var fsm = new ConversationStateMachine(
            wakeWord: "elara",
            processingSilence: TimeSpan.FromMilliseconds(200),
            endSilence: TimeSpan.FromMilliseconds(500),
            log,
            time);

        fsm.HandleTranscription(t0, "Hello Elara", meaningful: true);

        Assert.Equal(ConversationMode.Listening, fsm.Mode);
        Assert.NotNull(fsm.ListeningSince);
        Assert.NotNull(fsm.LastHeardAt); // remainder of utterance buffered immediately after wake
    }

    [Fact]
    public void Same_Utterance_Wake_And_Question_Is_Preserved_In_Prompt()
    {
        var log = new TestLog();
        var t0 = DateTimeOffset.UtcNow;
        var time = new ManualTimeProvider(t0);
        var fsm = new ConversationStateMachine(
            wakeWord: "margaret",
            processingSilence: TimeSpan.FromMilliseconds(50),
            endSilence: TimeSpan.FromMilliseconds(500),
            log,
            time);

        string? prompt = null;
        fsm.PromptReady += p => prompt = p;

        // Single snippet includes wake word and question
        fsm.HandleTranscription(t0, "Hey Margaret, tell me about Greek cuisine", meaningful: true);

        // Advance time past processing silence to trigger prompt
        fsm.Tick(t0.AddMilliseconds(80));

        Assert.Equal(ConversationMode.Processing, fsm.Mode);
        Assert.Equal("Hey Margaret, tell me about Greek cuisine", prompt);
    }

    [Fact]
    public void Buffers_And_Emits_Prompt_On_Processing_Silence()
    {
        var log = new TestLog();
        var t0 = DateTimeOffset.UtcNow;
        var time = new ManualTimeProvider(t0);
        var fsm = new ConversationStateMachine(
            wakeWord: "hey",
            processingSilence: TimeSpan.FromMilliseconds(50),
            endSilence: TimeSpan.FromMilliseconds(500),
            log,
            time);

        string? prompt = null;
        fsm.PromptReady += p => prompt = p;

        // Wake
        fsm.HandleTranscription(t0, "hey there", meaningful: true);
        Assert.Equal(ConversationMode.Listening, fsm.Mode);

        // Speak two snippets while listening
        fsm.HandleTranscription(t0.AddMilliseconds(10), "how are", meaningful: true);
        fsm.HandleTranscription(t0.AddMilliseconds(20), "you?", meaningful: true);

        // Advance time past processing silence
        fsm.Tick(t0.AddMilliseconds(70));

        Assert.Equal(ConversationMode.Processing, fsm.Mode);
        Assert.Equal("hey there how are you?", prompt);

        // End processing goes back to Listening
        fsm.EndProcessing();
        Assert.Equal(ConversationMode.Listening, fsm.Mode);
    }

    [Fact]
    public void Listening_To_Quiescent_On_Extended_Silence()
    {
        var log = new TestLog();
        var t0 = DateTimeOffset.UtcNow;
        var time = new ManualTimeProvider(t0);
        var fsm = new ConversationStateMachine(
            wakeWord: "elara",
            processingSilence: TimeSpan.FromMilliseconds(50),
            endSilence: TimeSpan.FromMilliseconds(120),
            log,
            time);

        fsm.HandleTranscription(t0, "elara", meaningful: true); // wake to listening
        Assert.Equal(ConversationMode.Listening, fsm.Mode);

        // No further meaningful input; tick beyond EndSilence
        fsm.Tick(t0.AddMilliseconds(130));
        Assert.Equal(ConversationMode.Quiescent, fsm.Mode);
    }

    [Fact]
    public void Speaking_Begin_And_End_Transitions()
    {
        var log = new TestLog();
        var t0 = DateTimeOffset.UtcNow;
        var time = new ManualTimeProvider(t0);
        var fsm = new ConversationStateMachine(
            wakeWord: "elara",
            processingSilence: TimeSpan.FromMilliseconds(50),
            endSilence: TimeSpan.FromMilliseconds(200),
            log,
            time);
        fsm.HandleTranscription(t0, "elara", meaningful: true); // to listening
        Assert.Equal(ConversationMode.Listening, fsm.Mode);

        fsm.BeginSpeaking();
        Assert.Equal(ConversationMode.Speaking, fsm.Mode);
        Assert.True(fsm.IsSpeaking);

        fsm.EndSpeaking();
        Assert.Equal(ConversationMode.Listening, fsm.Mode);
        Assert.False(fsm.IsSpeaking);
    }
}
