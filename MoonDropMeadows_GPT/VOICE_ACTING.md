# Optional Voice Acting

MoonDrop Meadows works perfectly well with no recorded voices. Text dialogue is always available, and voice acting can be enabled or disabled independently.

## Planned workflow

1. Open Voice Studio from Settings.
2. Pick a character and a line from the dialogue library.
3. Record a WAV take, preview it, keep it, or record again.
4. The game stores the recording locally. It is never uploaded automatically.
5. Players who do not record anything continue using text dialogue and optional placeholder sounds.

## File layout

    VoiceActing/
      manifest.json
      bob/
        greeting_01.wav
        moonwell_01.wav
      mayorlarry/
        greeting_01.wav
      juniper/
        greeting_01.wav

Use mono PCM WAV, 16-bit, 22,050 Hz or 44,100 Hz. Keep lines short and leave a little silence at the end.

## Privacy and permissions

- Recording is optional.
- Microphone access is requested only when the player opens the recorder.
- Show a recording indicator while the microphone is active.
- Audio stays local unless the player explicitly exports or shares it.
- A missing or invalid voice file never prevents dialogue from working.

The prototype is silent by default. The in-game microphone recorder and take-management screen are planned for a later audio milestone.
