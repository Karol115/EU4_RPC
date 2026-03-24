# Discord RPC for EU4

Works with **all versions** of EU4 (Steam, Epic Games, etc.).  
This is a separate app, so you need to launch it when starting EU4.
For more autmatication use **build-in bat+shortcut generator** or create a `.bat` file to launch both apps (example below).  
Closes automatically when EU4 is closed.

## .bat example

```bat
@echo off
start "" "{eu4_path}"
start "" "{eu4-RPC_path}"
```

## Optional
When you are creating bat file manually this can be useful: create shortcut to .bat and change icon(Original exe shortcut->Change Icon->copy path and paste to shortcut icon for .bat).
Done!!!