# 📢 TelegramSupportBot  

A **Telegram bot** built with **C# (.NET)** that allows sending announcements and messages to multiple groups.  
It supports **interactive group selection**, **file/photo/document distribution**, and automatically manages the list of groups where the bot is added.  

---

## 🚀 Features  

- Add bot to multiple groups and send announcements  
- `/start` – Display help and available commands  
- `/groups` – Show list of connected groups  
- `/send` – Prepare and send a message (text, photo, or document)  
- Group selection with **inline keyboard**  
- Option to **send to all groups** at once  
- Persistent group storage in `groups.txt`  
- Checks admin rights before sending messages  
- Handles cases when the bot is removed or loses admin rights  

---

## 🛠️ Technology Stack  

- **.NET 8 / C#**  
- **Telegram.Bot** library  
- File-based persistence (`groups.txt`)  

---

## 📂 Project Structure  

```
src/
├── Program.cs        # Main bot logic
├── PendingMessage.cs # Message model
├── groups.txt        # Stores group IDs
```

---

## ⚙️ Setup  

### 1. Clone repository  

```bash
git clone https://github.com/OneWay2Go/PDPTelegramSupportBot.git
cd PDPTelegramSupportBot
```

### 2. Install dependencies  

Make sure you have **.NET 8 SDK** installed.  
Then restore dependencies:  

```bash
dotnet restore
```

### 3. Configure bot token  

Replace the placeholder in `Program.cs`:  

```csharp
string token = "TELEGRAM_BOT_TOKEN";
```

with your bot token from [BotFather](https://t.me/botfather).  

### 4. Run the bot  

```bash
dotnet run
```

---

## 📖 Commands  

| Command    | Description |
|------------|-------------|
| `/start`   | Show help menu |
| `/groups`  | List groups where the bot is added |
| `/send`    | Prepare and send a message (text, photo, or document) |

---

## 📦 Group Management  

- When the bot is added to a group, it saves the **Group ID** in `groups.txt`.  
- If the bot is removed or loses admin rights, the group is automatically removed from the list.  

---

## 🔒 Requirements  

- .NET 8 SDK  
- A Telegram Bot token (from **@BotFather**)  

---

## 👤 Author  

Developed by **Uchqunov Muhammadamin**  
🔗 GitHub: [OneWay2Go](https://github.com/OneWay2Go)  
