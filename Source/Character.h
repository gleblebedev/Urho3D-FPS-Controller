//
// Copyright (c) 2008-2016 the Urho3D project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

#pragma once

#include <Urho3D/Input/Controls.h>
#include <Urho3D/Scene/LogicComponent.h>

#include <Urho3D/Container/Ptr.h>
#include <Urho3D/Physics/RigidBody.h>
#include <Urho3D/Math/Vector3.h>
#include <Urho3D/Graphics/AnimatedModel.h>
#include <Urho3D/Scene/Node.h>

using namespace Urho3D;

const int CTRL_FORWARD = 1;
const int CTRL_BACK = 2;
const int CTRL_LEFT = 4;
const int CTRL_RIGHT = 8;
const int CTRL_JUMP = 16;
const int CTRL_SHIFT = 32;
const int CTRL_CROUCH = 64;

const float MOVE_FORCE = 60.0f;
const float MOVE_ACCELERATION = 2.0f;
const float MOVE_DECELERATION = 4.0f;
const float INAIR_MOVE_FORCE = 60.0f;
const float BRAKE_FORCE = 6.0f;
const float INAIR_BRAKE_FORCE = 6.0f;
const float AGILITY = 155.0f;
const float INAIR_AGILITY = 1.5f;
const float JUMP_FORCE = 350.0f;
const float JUMP_RECOVER = 0.5f;
const float CROUCH_SPEED = 15.0f;
const float CROUCH_RECOVER = 0.1f;
const float YAW_SENSITIVITY = 0.1f;
const float INAIR_THRESHOLD_TIME = 0.1f;

/// Character component, responsible for physical movement according to controls, as well as animation.
class Character : public LogicComponent
{
    URHO3D_OBJECT(Character, LogicComponent);

public:
    /// Construct.
    Character(Context* context);
    
    /// Register object factory and attributes.
    static void RegisterObject(Context* context);
    
    /// Handle startup. Called by LogicComponent base class.
    virtual void Start();
    virtual void DelayedStart();

    /// Handle physics world update. Called by LogicComponent base class.
    virtual void FixedUpdate(float timeStep);
    
    /// Movement controls. Assigned by the main program each frame.
    Controls controls_;

    WeakPtr<Node> modelNode_;
    
private:
    /// Handle physics collision event.
    void HandleNodeCollision(StringHash eventType, VariantMap& eventData);
    void HandleRenderUpdate(StringHash eventType, VariantMap& eventData);
    
    /// Grounded flags for movement.
    bool onGround_;
    bool nearGround_;
    bool dropDetected_;
    
    /// Jump flags.
    bool okToJump_;
    bool jumpReleased_;

    /// In air timer. Due to possible physics inaccuracy, character can be off ground for max. 1/10 second and still be allowed to move.
    float inAirTimer_;
    float jumpTimer_;
    bool jumped_;
    
    /// Crouch flags
    bool crouching_;
    bool okToStand_;
    float crouchTimer_;

    /// Movement factors
    float moveMag_;
    Vector3 velocity_;

    /// Bug suppression
    bool highImpulseDetected_;

    /// Components
    WeakPtr<RigidBody> rigidBody_;
};
