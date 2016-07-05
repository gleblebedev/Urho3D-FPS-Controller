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

#include <Urho3D/Core/Context.h>
#include <Urho3D/Graphics/AnimationController.h>
#include <Urho3D/IO/MemoryBuffer.h>
#include <Urho3D/Physics/PhysicsEvents.h>
#include <Urho3D/Physics/PhysicsWorld.h>
#include <Urho3D/Physics/RigidBody.h>
#include <Urho3D/Physics/CollisionShape.h>
#include <Urho3D/Scene/Scene.h>
#include <Urho3D/Scene/SceneEvents.h>
#include <Urho3D/Math/Quaternion.h>
#include <Urho3D/Resource/ResourceCache.h>
#include <Urho3D/Graphics/GraphicsEvents.h>
#include <Urho3D/Resource/XMLFile.h>
#include <Urho3D/Resource/XMLElement.h>
#include <Urho3D/IO/Log.h>
#include <Urho3D/Math/MathDefs.h>

#include "Character.h"

Character::Character(Context* context) :
    LogicComponent(context),
    modelNode_(nullptr),
    rigidBody_(nullptr),
    onGround_(false),
    nearGround_(false),
    dropDetected_(true),
    okToJump_(true),
    jumpReleased_(true),
    inAirTimer_(0.0f),
    jumpTimer_(0.0f),
    jumped_(false),
    crouching_(false),
    crouchTimer_(0.0f),
    okToStand_(true),
    moveMag_(0.0f),
    velocity_(0.0f, 0.0f, 0.0f),
    highImpulseDetected_(false)
{
    // Only the physics update event is needed: unsubscribe from the rest for optimization
    SetUpdateEventMask(USE_FIXEDUPDATE);
}

void Character::RegisterObject(Context* context)
{
    context->RegisterFactory<Character>();

    // These macros register the class attributes to the Context for automatic load / save handling.
    // We specify the Default attribute mode which means it will be used both for saving into file, and network replication
    URHO3D_ATTRIBUTE("Controls Yaw", float, controls_.yaw_, 0.0f, AM_DEFAULT);
    URHO3D_ATTRIBUTE("Controls Pitch", float, controls_.pitch_, 0.0f, AM_DEFAULT);
    URHO3D_ATTRIBUTE("On Ground", bool, onGround_, false, AM_DEFAULT);
    URHO3D_ATTRIBUTE("OK To Jump", bool, okToJump_, true, AM_DEFAULT);
    URHO3D_ATTRIBUTE("In Air Timer", float, inAirTimer_, 0.0f, AM_DEFAULT);
}

void Character::Start()
{
    // Component has been inserted into its scene node. Subscribe to events now
    SubscribeToEvent(E_NODECOLLISION, URHO3D_HANDLER(Character, HandleNodeCollision));
    SubscribeToEvent(GetNode(), E_BEGINRENDERING, URHO3D_HANDLER(Character, HandleRenderUpdate));
}

void Character::DelayedStart()
{
    Scene* scene = GetScene();
    if (scene)
    {
        Node* node = GetNode();
        XMLFile* xmlFile = GetSubsystem<ResourceCache>()->GetResource<XMLFile>("Objects/CharacterModel.xml");
        if (xmlFile)
        {
            XMLElement xmlElement = xmlFile->GetRoot();
            modelNode_ = scene->InstantiateXML(xmlElement, node->GetPosition(), node->GetRotation());
        }
        else
        {
            URHO3D_LOGERROR("Couldn't load character, couldn't find CharacterModel.xml !");
        }
    }
    else
    {
        URHO3D_LOGERROR("Couldn't load character, scene is null !");
        modelNode_ = NULL;
    }

    if (!modelNode_)
        URHO3D_LOGERROR("Couldn't create character model node !");

    URHO3D_LOGINFO("Finished Loading Character.");
}

void Character::FixedUpdate(const float timeStep)
{
    if (!rigidBody_)
        rigidBody_ = GetComponent<RigidBody>();
    AnimationController* animCtrl(modelNode_->GetComponent<AnimationController>());

    jumpTimer_ += timeStep;
    crouchTimer_ += timeStep;

    // Update the in air timer. Reset if grounded
    if (!onGround_)
        inAirTimer_ += timeStep;
    else
        inAirTimer_ = 0.0f;

    // When character has been in air less than 1/10 second, it's still interpreted as being on ground
    const bool softGrounded(inAirTimer_ < INAIR_THRESHOLD_TIME);

    // Update movement & animation
    const Quaternion& rot(node_->GetRotation());
    Vector3 moveDir(Vector3::ZERO);
    const Vector3& velocity(rigidBody_->GetLinearVelocity());
    // Velocity on the XZ plane
    const Vector3 planeVelocity(velocity.x_, 0.0f, velocity.z_);
    const float speed(planeVelocity.Length());

    if (controls_.IsDown(CTRL_FORWARD))
        moveDir += Vector3::FORWARD * 1.0f;
    if (controls_.IsDown(CTRL_BACK))
        moveDir += Vector3::BACK * 0.6f;
    if (controls_.IsDown(CTRL_LEFT))
        moveDir += Vector3::LEFT * 0.75f;
    if (controls_.IsDown(CTRL_RIGHT))
        moveDir += Vector3::RIGHT * 0.75f;

    // Normalize move vector so that diagonal strafing is not faster
    if (moveDir.LengthSquared() > 1.0f)
        moveDir.Normalize();

    if (controls_.IsDown(CTRL_SHIFT))
        moveDir *= 0.4f;

    // Crouching
    const WeakPtr<CollisionShape> shape(GetComponent<CollisionShape>());
    
    const WeakPtr<Node> dropDetectNode(GetNode()->GetChild("DropDetect"));
    const WeakPtr<CollisionShape> dropDetectShape(dropDetectNode->GetComponent<CollisionShape>());

    const WeakPtr<Node> standDetectNode(GetNode()->GetChild("StandDetect"));
    const WeakPtr<CollisionShape> standDetectShape(standDetectNode->GetComponent<CollisionShape>());

    const WeakPtr<Node> groundDetectNode(GetNode()->GetChild("GroundDetect"));
    const WeakPtr<CollisionShape> groundDetectShape(groundDetectNode->GetComponent<CollisionShape>());


    const bool crouch((controls_.IsDown(CTRL_CROUCH) && crouchTimer_ >= CROUCH_RECOVER) || !okToStand_);// || (!softGrounded && crouching_));

    rigidBody_->DisableMassUpdate();

    if (crouch)
    {
        if (softGrounded)
            moveDir *= 0.3f;

        dropDetectShape->SetPosition(dropDetectShape->GetPosition().Lerp(Vector3(0.0f, 1.0f, 0.0f), timeStep * CROUCH_SPEED));
        standDetectShape->SetPosition(standDetectShape->GetPosition().Lerp(Vector3(0.0f, 2.1f, 0.0f), timeStep * CROUCH_SPEED));
        groundDetectShape->SetPosition(groundDetectShape->GetPosition().Lerp(Vector3(0.0f, 1.3f, 0.0f), timeStep * CROUCH_SPEED));

        shape->SetPosition(shape->GetPosition().Lerp(Vector3(0.0f, 1.5f, 0.0f), timeStep * CROUCH_SPEED));
        shape->SetSize(shape->GetSize().Lerp(Vector3(0.8f, 1.0f, 0.8f), timeStep * CROUCH_SPEED));

        crouching_ = true;
    }
    else
    {
        dropDetectShape->SetPosition(dropDetectShape->GetPosition().Lerp(Vector3(0.0f, 0.0f, 0.0f), timeStep * CROUCH_SPEED));
        standDetectShape->SetPosition(standDetectShape->GetPosition().Lerp(Vector3(0.0f, 1.1f, 0.0f), timeStep * CROUCH_SPEED));
        groundDetectShape->SetPosition(groundDetectShape->GetPosition().Lerp(Vector3(0.0f, 0.3f, 0.0f), timeStep * CROUCH_SPEED));

        const Vector3 pos(shape->GetPosition());
        Vector3 newPos(pos.Lerp(Vector3(0.0f, 1.0f, 0.0f), timeStep * CROUCH_SPEED));

        //  Adjust rigid body position to prevent penetration into the ground when standing
        if (softGrounded && !dropDetected_)
        {
            rigidBody_->SetPosition(rigidBody_->GetPosition() - (newPos - pos) * 2.0f);
        }
        else if (nearGround_)
        {
            rigidBody_->SetPosition(rigidBody_->GetPosition() - (newPos - pos) * 3.0f);
        }

        shape->SetPosition(newPos);
        shape->SetSize(shape->GetSize().Lerp(Vector3(0.8f, 2.0f, 0.8f), timeStep * CROUCH_SPEED));


        if (crouching_)
            crouchTimer_ = 0.0f;

        crouching_ = false;
    }

    rigidBody_->EnableMassUpdate();

    // Prevent bounce bugs when colliding against corners, while still allowing movement up ramps
    if (highImpulseDetected_ && nearGround_)
    {
        const Vector3 vel(rigidBody_->GetLinearVelocity());
        rigidBody_->SetLinearVelocity(Vector3(vel.x_, Min(vel.y_, 0.0f), vel.z_));
    }

    // If in air, allow control, but slower than when on ground
    if (softGrounded && okToJump_)
    {
        const float moveMag(moveDir.Length());
        if (moveMag > 0.0001)
            moveMag_ = Lerp(moveMag_, 1.0f, timeStep * MOVE_ACCELERATION);
        else
            moveMag_ = Lerp(moveMag_, 0.0f, timeStep * MOVE_DECELERATION);

        velocity_ = velocity_.Lerp(rot * moveDir * moveMag_ * MOVE_FORCE, timeStep * AGILITY);
        rigidBody_->ApplyImpulse(velocity_);
        const Vector3 brakeForce(planeVelocity * -BRAKE_FORCE);
        rigidBody_->ApplyImpulse(brakeForce);
    }
    else
    {
        if (!(dropDetected_ && nearGround_))
        {
            velocity_ = velocity_.Lerp(rot * moveDir * moveMag_ * INAIR_MOVE_FORCE, timeStep * INAIR_AGILITY);
            rigidBody_->ApplyImpulse(velocity_);
            const Vector3 brakeForce(planeVelocity * -INAIR_BRAKE_FORCE);
            rigidBody_->ApplyImpulse(brakeForce);
        }
    }

    // Jumping
    if (!controls_.IsDown(CTRL_JUMP))
        jumpReleased_ = true;

    if (!okToJump_ && jumpTimer_ > JUMP_RECOVER && onGround_ && softGrounded && jumpReleased_)
        okToJump_ = true;

    if (softGrounded && !highImpulseDetected_)
    {
        if (controls_.IsDown(CTRL_JUMP) && okToJump_)
        {
            const Vector3 vel(rigidBody_->GetLinearVelocity());
            URHO3D_LOGINFO("JUMP " + String(vel.y_));
            rigidBody_->SetLinearVelocity(Vector3(vel.x_, Max(vel.y_, 0.0f), vel.z_));
            rigidBody_->ApplyImpulse(Vector3::UP * JUMP_FORCE);
            okToJump_ = false;
            jumpTimer_ = 0.0f;
            jumpReleased_ = false;
        }
    }

    // Keep the character close the ground / prevent launching from high speed movement
    if ((okToJump_ && inAirTimer_ > 0.0f && inAirTimer_ <= INAIR_THRESHOLD_TIME && speed > 0.1f && !dropDetected_)
        ||
        (inAirTimer_ > INAIR_THRESHOLD_TIME && !dropDetected_))
    {
        rigidBody_->ApplyImpulse(Vector3(0.0f, -9.81f * speed, 0.0f));
    }

    // Prevent sliding while on slopes
    rigidBody_->SetUseGravity(!onGround_);

    // Add artificial gravity after some time to pull to ground faster
    if (inAirTimer_ > 0.1f && velocity_.Length() > 0.1f)
        rigidBody_->ApplyForce(Vector3(0.0f, -9.81f * 2.0f, 0.0f));

    // Play walk animation if moving on ground, otherwise fade it out
    if (softGrounded && !moveDir.Equals(Vector3::ZERO))
        animCtrl->PlayExclusive("Models/Jack_Walk.ani", 0, true, 0.2f);
    else
        animCtrl->Stop("Models/Jack_Walk.ani", 0.2f);
    // Set walk animation speed proportional to velocity
    animCtrl->SetSpeed("Models/Jack_Walk.ani", speed / 9.0f);

    // Reset grounded flag for next frame
    onGround_ = false;
    nearGround_ = false;
    dropDetected_ = true;
    okToStand_ = true;
    highImpulseDetected_ = false;

    modelNode_->SetPosition(modelNode_->GetPosition().Lerp(
        rigidBody_->GetPosition() + rigidBody_->GetLinearVelocity() * timeStep * 4.0f,
        timeStep * (10.0f + rigidBody_->GetLinearVelocity().Length() / 8.0f
    )));

    modelNode_->SetRotation(rot);
}

void Character::HandleNodeCollision(StringHash eventType, VariantMap& eventData)
{
    // Check collision contacts and see if character is standing on ground (look for a contact that has near vertical normal)
    using namespace NodeCollision;

    const WeakPtr<RigidBody> body((RigidBody*)eventData["Body"].GetVoidPtr());
    const WeakPtr<Node> node(body->GetNode());
    const String name(node->GetName());
    const WeakPtr<RigidBody> otherBody((RigidBody*)eventData["OtherBody"].GetVoidPtr());
    const WeakPtr<Node> otherNode(otherBody->GetNode());
    const String otherName(otherNode->GetName());

    // Detect if there is ground within stepping range
    if (eventData["Trigger"].GetBool())
    {
        if (name == "DropDetect")
        {
            dropDetected_ = false;
            return;
        }

        if (name == "StandDetect")
        {
            okToStand_ = false;
            return;
        }

        if (name == "GroundDetect")
        {
            nearGround_ = true;
            return;
        }
    }

    // Filter out collisions with DropDetect as otherNode, for some reason this can cause a crouch-jump glitch ?
    if (otherName == "DropDetect")
    {
        // dropDetected_ = false;
        return;
    }

    if (otherName == "StandDetect")
        return;
    
    if (otherName == "GroundDetect")
        return;

    /*if (name != "Character")
        return;*/
    
    const Vector3 vel(body->GetLinearVelocity());
    const float YY(GetNode()->GetPosition().y_);
    const WeakPtr<CollisionShape> colShape(GetNode()->GetComponent<CollisionShape>());
    const float height(colShape->GetPosition().y_);

    MemoryBuffer contacts(eventData[P_CONTACTS].GetBuffer());

    while (!contacts.IsEof())
    {
        Vector3 contactPosition = contacts.ReadVector3();
        Vector3 contactNormal = contacts.ReadVector3();
        float contactDistance = contacts.ReadFloat();
        float contactImpulse = contacts.ReadFloat();

        // Prevent bounce from corner impact
        if (contactImpulse > 200.0f)
            highImpulseDetected_ = true;

        // If contact is below node center and mostly vertical, assume it's a ground contact
        if (contactPosition.y_ < YY + height)
        {
            float level = Abs(contactNormal.y_);
            if (level > 0.75)
                onGround_ = true;
        }
    }
}

void Character::HandleRenderUpdate(StringHash eventType, VariantMap& eventData)
{
    const float timeStep(eventData["TimeStep"].GetFloat());
    //
}
