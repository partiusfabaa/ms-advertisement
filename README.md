# ms-advertisement

A plugin for cs that allows you to show ads in chat/center

# Preview
Chat:

<img width="306" height="67" alt="image" src="https://github.com/user-attachments/assets/8d8b7e7d-e5b8-4ce9-807b-4e5debd053b7" />

Center:

<img width="341" height="118" alt="image" src="https://github.com/user-attachments/assets/b6488451-609d-4e09-9a53-5122775278e6" />

# Installation
1. Install [ModSharp](https://github.com/Kxnrl/modsharp-public)
2. Download [Advertisement](https://github.com/partiusfabaa/ms-advertisement/releases)
3. Unzip the archive and upload it to the game server

# Config
The config is created automatically
```json
[
  {
    "Interval": 5,
    "Messages": [
      {
        "Chat": "{info_status}",
        "Center": "{center_date}"
      },
      {
        "Center": "{info_server}"
      },
      {
        "Chat": "{chat_social}"
      }
    ]
  }
]
```

# Localization
The plugin supports translations, but they are optional. You can write raw text directly in the config if you prefer.

To use the translation system:
Define your translations in the language file.

Use the key format {KEY_NAME} in your Advertisement.json.

### Example:
If you have "my_message" in your lang file, put {my_message} in the config.

# CHAT COLORS: 
{DEFAULT}, {RED}, {LIGHTPURPLE}, {GREEN}, {LIME}, {LIGHTGREEN}, {LIGHTRED}, {GRAY}, {LIGHTOLIVE}, {OLIVE}, {LIGHTBLUE}, {BLUE}, {PURPLE}, {GRAYBLUE}

# TAGS:

	{MAP} 	- current map
  
	{TIME} 	- server time
  
	{DATE} 	- current date
  
	{IP} - server ip
  
	{PORT} - server port
  
	{PLAYERS} - number of players on the server
  
	{MAXPLAYERS} - how many slots are available on the server
  
	\n OR {N}		- new line
