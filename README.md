# SimpleRecorder

## What it is:
A simple, barebones, "just works" audio recorder for capturing combined audio recording of microphone and speakers.

## How to install

### Prerequisites

The project is currently build with .Net 9. Download the .Net 9 SDK if you are building from source. Download the .Net 9 runtime if you want to use the prebuilt binaries. 

### Building

Currently requires .Net 9 SDK. Install the SDK. Clone the repo and run `dotnet build` in the folder.

### Download

Install the .Net 9 runtime. Download the latest binaries from the releases.

### How it works

The application is a console-based application. Running the `.SimpleRecorder.exe` file will launch a console window. The first time you run the application, it will ask you to choose an input device. Find your desired microphone in the list, type the id number, press enter, and Y to confirm. 

The application will start recording and report the elapsed time.  **NOTE:** You must keep the console window open for the duration of recording. 

Press the console window escape keys `Ctrl+C` to stop recording. To relaunch the app, close the console window and re-launch the .exe.

Your recordings can be found in the `<user>\Music\SimpleRecorder` folder.

After the first run, the app stores your mic device preference and will immediately begin recording once launched.

The app is designed to be extremely fast to start recording on subsequent uses after the initial setup.
#### **Note: closing the console window by pressing the X window button is not guaranteed to result in a successful saved recording. The `Ctrl+C` keypress is the intended means to save and close.**


### Technical

The app is built atop `NAudio` using `WasapiLoopbackCapture` and `WaveInEvent`.

All files are saved in the `<user>\Music\SimpleRecorder` folder. A `.config` hidden file is saved in the `<user>\AppData\Local\SimpleRecorder` folder.

The application records two temporary tracks, one for microphone sound, one for PC sound; the tracks are saved into the folder. Upon exiting the console window, the tracks are mixed into one `.wav` file and saved to `Music\SimpleRecorder` using ISO 8601 date format. 

**Note:** The app will possibly conflict with a software audio mixer like Voicemeeter.

---

### License

MIT License. 

### Attribution

Thanks to Mike Hadlow for his simple event loop. 

https://mikehadlow.com/posts/2021-07-09-simple-console-loop/

StackOverflow: Capture console exit.

https://stackoverflow.com/questions/474679/capture-console-exit-c-sharp