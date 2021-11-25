# VS Code Rhino Debug

A simple VS Code debugger extension for Rhinoceros based on [vscode-mono-debug](https://github.com/microsoft/vscode-mono-debug).

## Using the extension

Use a `launch.json` with the following:

```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Launch Rhinoceros",
            "type": "rhino",
            "request": "launch",
            "runtimeExecutable": "/Applications/Rhinoceros.app/Contents/MacOS/Rhinoceros",
            "env": {
                // optional: register your plugin(s) to load
                "RHINO_PLUGIN_PATH": "${workspaceFolder}/Path/To/MyPlugin.rhp",
                "GRASSHOPPER_PLUGINS": "${workspaceFolder}/Path/To/MyGHPlugin.gha"
            }
        }
    ]
}
```


## Building the rhino-debug extension

Building and using VS Code rhino-debug requires a basic POSIX-like environment, a Bash-like
shell, and an installed Mono framework.

First, clone the rhino-debug project:

```bash
$ git clone --recursive https://github.com/mcneel/vscode-rhino-debug
```

To build the extension vsix, run:

```bash
$ cd vscode-rhino-debug
$ npm install
$ make
```
