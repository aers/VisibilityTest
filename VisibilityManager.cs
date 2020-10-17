using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Dalamud.Hooking;
using Dalamud.Plugin;
using FFXIVClientStructs.Client.Game.Character;


namespace VisibilityTest
{
    public class VisibilityManager : IDisposable
    {
        private Plugin _plugin;

        // i used object ids for players, but you obviously can't do that for anything that has the default E0000000 object ID, so use pointers probably
        private HashSet<uint> HiddenPlayerObjectIds = new HashSet<uint>();

        private HashSet<uint> ObjectIdsToUnhide = new HashSet<uint>();

        private unsafe BattleChara* LocalPlayer;

        // void Client::Game::Character::Character::EnableDraw(Client::Game::Character::Character* thisPtr);
        private unsafe delegate void CharacterEnableDrawPrototype(Character* thisPtr);
        // void Client::Game::Character::Character::DisableDraw(Client::Game::Character::Character* thisPtr);
        private unsafe delegate void CharacterDisableDrawPrototype(Character* thisPtr);
        // void Client::Game::Character::Companion::EnableDraw(Client::Game::Character::Companion* thisPtr);
        private unsafe delegate void CompanionEnableDrawPrototype(Companion* thisPtr);
        // void dtor_Client::Game::Character::Character(Client::Game::Character::Character* thisPtr);
        private unsafe delegate void CharacterDtorPrototype(Character* thisPtr);

        private Hook<CharacterEnableDrawPrototype> hookCharacterEnableDraw;
        private Hook<CharacterDisableDrawPrototype> hookCharacterDisableDraw;
        private Hook<CompanionEnableDrawPrototype> hookCompanionEnableDraw;
        private Hook<CharacterDtorPrototype> hookCharacterDtor;

        public VisibilityManager(Plugin _p)
        {
            _plugin = _p;
        }

        public unsafe void Init()
        {
            var scanner = _plugin.pluginInterface.TargetModuleScanner;

            // actually a pointer to the game's memory storage for all battlecharas. the player character is always the first.
            // this SHOULD persist on swapping characters but i didnt actually test
            var localPlayerAddress = scanner.GetStaticAddressFromSig("48 8B F8 48 8D 2D ? ? ? ? ");
            LocalPlayer = *(BattleChara**)localPlayerAddress.ToPointer();

            var characterEnableDrawAddress = scanner.ScanText("E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 85 C9 74 2C 33 D2");
            var characterDisableDrawAddress = scanner.ScanText("40 57 41 56 48 83 EC 28 48 8B F9 48 8B 0D ?? ?? ?? ??");
            var companionEnableDrawAddress = scanner.ScanText("40 53 48 83 EC 20 80 B9 ?? ?? ?? ?? ?? 48 8B D9 72 0C F7 81 ?? ?? ?? ?? ?? ?? ?? ?? 74 3D");
            var characterDtorAddress = scanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8D 05 ?? ?? ?? ?? 48 8B D9 48 89 01 48 8D 05 ?? ?? ?? ?? 48 89 81 ?? ?? ?? ?? 48 8B 89 ?? ?? ?? ??");

            this.hookCharacterEnableDraw = new Hook<CharacterEnableDrawPrototype>(characterEnableDrawAddress, new CharacterEnableDrawPrototype(CharacterEnableDrawDetour), this);
            this.hookCharacterDisableDraw = new Hook<CharacterDisableDrawPrototype>(characterDisableDrawAddress, new CharacterDisableDrawPrototype(CharacterDisableDrawDetour), this);
            this.hookCompanionEnableDraw = new Hook<CompanionEnableDrawPrototype>(companionEnableDrawAddress, new CompanionEnableDrawPrototype(CompanionEnableDrawDetour), this);
            this.hookCharacterDtor = new Hook<CharacterDtorPrototype>(characterDtorAddress, new CharacterDtorPrototype(CharacterDtorDetour), this);

            this.hookCharacterEnableDraw.Enable();
            this.hookCharacterDisableDraw.Enable();
            this.hookCompanionEnableDraw.Enable();
            this.hookCharacterDtor.Enable();
        }

        public void UnhidePlayers()
        {
            ObjectIdsToUnhide.UnionWith(HiddenPlayerObjectIds);
            HiddenPlayerObjectIds.Clear();
        }

        private unsafe void CharacterEnableDrawDetour(Character* thisPtr)
        {
            // do other checks here etc
            if (thisPtr != (Character*)LocalPlayer && thisPtr->GameObject.ObjectKind == 0x1 && _plugin.config.HidePlayers)
            {
                thisPtr->GameObject.RenderFlags |= (1 << 1 | 1 << 11);
                HiddenPlayerObjectIds.Add(thisPtr->GameObject.ObjectID);
            }            
            hookCharacterEnableDraw.Original(thisPtr);
        }

        private unsafe void CharacterDisableDrawDetour(Character * thisPtr)
        {
            // again, this will be more complicated when unhiding only friend players or whatever
            if (ObjectIdsToUnhide.Contains(thisPtr->GameObject.ObjectID))
            {
                thisPtr->GameObject.RenderFlags &= ~(1 << 1 | 1 << 11);
                ObjectIdsToUnhide.Remove(thisPtr->GameObject.ObjectID);
            }
            hookCharacterDisableDraw.Original(thisPtr);
        }

        // Companions (MINIONS) have their own enable draw override, while it calls the character one you need to set the flag before it does that for minion hiding properly
        private unsafe void CompanionEnableDrawDetour(Companion * thisPtr)
        {
            hookCompanionEnableDraw.Original(thisPtr);
        }

        // clear destroyed characters from our tracking sets
        // this is important when using pointers as a tracker, since pointers get re-used for characters
        private unsafe void CharacterDtorDetour(Character * thisPtr)
        {
            if (thisPtr->GameObject.ObjectKind == 0x1)
                HiddenPlayerObjectIds.Remove(thisPtr->GameObject.ObjectID);

            hookCharacterDtor.Original(thisPtr);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            this.hookCharacterEnableDraw.Disable();
            this.hookCharacterDisableDraw.Disable();
            this.hookCompanionEnableDraw.Disable();
            this.hookCharacterDtor.Dispose();

            this.hookCharacterEnableDraw.Dispose();
            this.hookCharacterDisableDraw.Dispose();
            this.hookCompanionEnableDraw.Dispose();
            this.hookCharacterDtor.Dispose();
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
