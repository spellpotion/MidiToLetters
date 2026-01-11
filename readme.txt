edit appsettings.json to modify functionality

"MidiDeviceIndex": - the index number of the MIDI device used, if not sure, launch the app and see what devices it detected. if your device is not listed, it can't be used.

"Mode": "Cycle" - black keys cycle between enharmonics when pressed multiple times

"Mode": "Ante" - black keys have value based on the note pressed before, if the note is lower, the #(sharp) version is used and vice versa

"Mode": "Post" - black keys don't output value by itself, instead value is outputted after a white key is pressed. if the note of the white key is higher, the b(flat) version of the black key is used and vice versa.

"Tap": "Combined" - also sends a space key down and space key up with with each letter

"Tap": "Disabled" - sends just the letters

"Tap": "Exclusive" - sends just the space key down and space key up

"Mappings" :
{
	"C": "a",
    "C#": "w",
    "Db": "z"
    …
}

array of default mappings, name is the note name, value is the emulated key.
17 English note names are used, there is no H or S note.