using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Talking NPC guide. Press N while in range to hear a line; each talk shows a
// different line (cycling through a shuffled-once-per-NPC order) so repeat
// conversations don't just repeat the same sentence. A couple of the lines
// quote the game's actual win-condition numbers, pulled live from
// GameScoreManager (ProtonsPerCoin/ElectronsPerCoin/CoinsToWin/EnemiesToWin)
// instead of being hardcoded, so they can never drift out of sync if those
// values are re-tuned in the Inspector. N is dedicated to NPC talk; E is
// reserved globally for the Instructions panel (see GameScoreManager) so the
// two never collide.
public sealed class NpcDialogue : MonoBehaviour
{
    private const string Prompt = "Press N to talk";

    // Flavor/personality lines that don't depend on any win-condition numbers.
    private static readonly string[] FlavorLines =
    {
        "I heard Electrons are hard to find these days.",
        "I heard Protons are easier to find than Electrons.",
        "What is your name? My name is Trip.",
        "Watch out for the poison gas clouds - they don't care how tough you are.",
        "That red car parked nearby is faster than running. Walk up to it and press I to drive.",
        "Press U to cycle the compass between the nearest enemy, proton, electron, or vehicle.",
        "Kill an enemy and another one shows up nearby soon after. They don't give up.",
        "Keep an eye on the clock - if it hits zero before you're done, that's a loss.",
        "Guns you pick up go straight into your chest inventory. Press Y to check it.",
        "Coins don't just lie around - you have to craft them yourself.",
        "Press E anytime to pull up every control and the win condition in one place."
    };

    // Cached once and reused by every NPC - a soft double-blip distinct from
    // the pickup chimes and the gunshot/defeat sounds.
    private static AudioClip talkClip;

    private GameScoreManager game;
    private bool playerInRange;
    private string[] lines;
    private int lineIndex;
    private AudioSource talkAudioSource;

    public void Initialize(GameScoreManager manager)
    {
        game = manager;
    }

    private void Start()
    {
        if (game == null)
        {
            game = FindAnyObjectByType<GameScoreManager>();
        }

        BuildLines();
    }

    // Builds this NPC's line set once: the shared flavor lines plus two lines
    // whose numbers are read straight from GameScoreManager, then shuffles the
    // combined order so different NPCs don't all recite lines in lockstep.
    private void BuildLines()
    {
        string craftLine = game != null
            ? $"I heard {game.ProtonsPerCoin} Protons + {game.ElectronsPerCoin} Electrons create a Coin."
            : "I heard Protons and Electrons combine into a Coin.";

        string winLine = game != null
            ? $"Word is you need {game.CoinsToWin} Coins crafted and {game.EnemiesToWin} enemies defeated to win."
            : "Word is you need enough Coins crafted and enemies defeated to win.";

        List<string> combined = new List<string>(FlavorLines) { craftLine, winLine };

        for (int i = combined.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (combined[i], combined[j]) = (combined[j], combined[i]);
        }

        lines = combined.ToArray();
    }

    private void Update()
    {
        if (playerInRange && WasTalkKeyPressed() && game != null && lines != null && lines.Length > 0)
        {
            game.ShowDialogue($"Guide: {lines[lineIndex]}");
            PlayTalkSound();
            lineIndex = (lineIndex + 1) % lines.Length;
        }
    }

    private void PlayTalkSound()
    {
        if (talkClip == null)
        {
            talkClip = RuntimeSfx.CreateTalkBlip("NPC Talk Blip");
        }

        if (talkAudioSource == null)
        {
            talkAudioSource = RuntimeSfx.GetOrAddOneShotSource(gameObject, volume: 0.5f);
        }

        talkAudioSource.PlayOneShot(talkClip);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<ThirdPersonController>() == null)
        {
            return;
        }

        playerInRange = true;
        game.SetPrompt(Prompt);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<ThirdPersonController>() == null)
        {
            return;
        }

        playerInRange = false;
        game.ClearPrompt(Prompt);
    }

    private static bool WasTalkKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetKeyDown(KeyCode.N);
    }
}
