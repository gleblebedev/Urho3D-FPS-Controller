#include <Urho3D/AngelScript/APITemplates.h>
#include <Urho3D/AngelScript/ScriptFile.h>
#include <Urho3D/AngelScript/Script.h>

#include "GamePlayer.h"
#include "Character.h"

void GamePlayer::SetupScript()
{
    const Script* script(GetSubsystem<Script>());
    asIScriptEngine* const engine(script->GetScriptEngine());

    RegisterComponent<Character>(engine, "Character", true);
    engine->RegisterObjectProperty("Character", "Controls controls", offsetof(Character, controls_));
    engine->RegisterObjectProperty("Character", "Node@ modelNode", offsetof(Character, modelNode_));
}