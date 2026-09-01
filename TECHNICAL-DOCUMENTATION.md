# Technical Documentation

## Summary

Terraria-RPC reads Terraria's live process memory so it can show real-time status data in Discord Rich Presence.

Administrator mode is used because Windows can block or limit access to another process depending on how both apps were started. Running elevated makes the attachment step more reliable and reduces the chance of partial reads, missing fields, or failed updates.

## What this means in practice

Terraria-RPC does not hook into the game or modify it.

It reads data from the running Terraria process, then formats that data into Discord status text and icons. That includes things like:

- player HP, mana, defense, and weapon damage
- current world name, biome, and difficulty
- bosses, events, peaceful events, and weather
- state that changes while the game is running

If the process cannot be opened with enough access, the reader may fail to attach or may only read part of the data it needs. That is why elevation is recommended.

## More detailed explanation

The project uses CLRMD to inspect Terraria's managed memory. That means it looks at the live process and reads runtime objects and fields instead of relying on a game mod or a network API.

On Windows, process access can vary based on:

- whether Terraria was started normally or elevated
- whether Terraria-RPC was started normally or elevated
- user account control restrictions
- process ownership and access rights

When both apps run at the same privilege level, the attachment process is usually fine. When they do not, Windows may deny the handle request or limit what can be read. In those cases, running Terraria-RPC as administrator avoids the mismatch.

This is a reliability requirement, not a gameplay requirement. Terraria-RPC is meant to stay local to your machine and only uses the memory it reads to update Discord Rich Presence.

## Practical notes

- If Terraria is launched normally and Terraria-RPC is launched normally, it may still work.
- If attachment fails, launching Terraria-RPC as administrator is the first thing to try.
- The app does not need administrator mode to control Terraria. It only needs it to read process memory more consistently.
