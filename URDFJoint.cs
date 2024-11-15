/*
    Author(s):  Long Qian
    Created on: 2019-03-29
    (C) Copyright 2015-2018 Johns Hopkins University (JHU), All Rights Reserved.

    --- begin cisst license - do not edit ---
    This software is provided "as is" under an open source license, with
    no warranty.  The complete license can be found in license.txt and
    http://www.cisst.org/cisst/license.txt.
    --- end cisst license ---
*/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace DVRK
{

    public class URDFJoint : MonoBehaviour
    {

        public Vector3 RPY, XYZ;

        public enum JointType { Fixed, Revolute, Continuous, Prismatic }
        public JointType jointType = JointType.Fixed;

        // non-fixed
        public enum JointAxis { X, Y, Z }
        public JointAxis jointAxis = JointAxis.Z;
        public GameObject jointObject;
        public float defaultJointValue;
        private Vector3 jointOperator;
        public bool independent = true;
        private List<URDFJoint> mimicJoints = new List<URDFJoint>();

        // revolute prismatic joint
        public Vector2 jointLimit;
        // mimic joint
        public float mimicFactor = 1f;
        public URDFJoint mimicParent;

        public float currentJointValue;


        private const float delta = 0.005f;

        private UDPClient udpClient;

        public static Quaternion QuaternionFromMatrix(Matrix4x4 m)
        {
            // Trap the case where the matrix passed in has an invalid rotation submatrix.
            if (m.GetColumn(2) == Vector4.zero)
            {
                Debug.Log("Quaternion got zero matrix");
                return Quaternion.identity;
            }
            return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
        }

        public static Vector3 PositionFromMatrix(Matrix4x4 m)
        {
            return m.GetColumn(3);
        }

        void Start()
        {
            // Find the UDPClient in the scene and assign it
            udpClient = FindObjectOfType<UDPClient>();

            if (udpClient == null)
            {
                Debug.LogError("UDPClient not found in the scene.");
            }
        }

        void Awake()
        {
            Matrix4x4 t = Matrix4x4.identity;
            t.SetColumn(3, new Vector4(XYZ.x, XYZ.z, XYZ.y, 1));


            Quaternion ry = Quaternion.Euler(0, -RPY.z, 0);
            Quaternion rz = Quaternion.Euler(0, 0, -RPY.y);
            Quaternion rx = Quaternion.Euler(-RPY.x, 0, 0);

            Matrix4x4 mry = Matrix4x4.TRS(Vector3.zero, ry, Vector3.one);
            Matrix4x4 mrz = Matrix4x4.TRS(Vector3.zero, rz, Vector3.one);
            Matrix4x4 mrx = Matrix4x4.TRS(Vector3.zero, rx, Vector3.one);

            Matrix4x4 final = t * mry * mrz * mrx;

            Quaternion quat = QuaternionFromMatrix(final);
            Vector3 eu = quat.eulerAngles;
            Vector3 pos = PositionFromMatrix(final);

            transform.localPosition = pos;
            transform.localRotation = quat;

            // Debug.Log(name + ": " + string.Format("{0:0.0000}, {1:0.0000}, {2:0.0000}", pos.x, pos.y, pos.z));
            // Debug.Log(name + ": " + string.Format("{0:0.0000}, {1:0.0000}, {2:0.0000}", eu.x, eu.y, eu.z));

            switch (jointAxis)
            {
                case JointAxis.X:
                    if (jointType == JointType.Prismatic)
                    {
                        jointOperator = new Vector3(1f, 0f, 0f);
                    }
                    else
                    {
                        jointOperator = new Vector3(-1f, 0f, 0f);
                    }
                    break;
                case JointAxis.Y:
                    if (jointType == JointType.Prismatic)
                    {
                        jointOperator = new Vector3(0f, 0f, 1f);
                    }
                    else
                    {
                        jointOperator = new Vector3(0f, 0f, -1f);
                    }
                    break;
                case JointAxis.Z:
                    if (jointType == JointType.Prismatic)
                    {
                        jointOperator = new Vector3(0f, 1f, 0f);
                    }
                    else
                    {
                        jointOperator = new Vector3(0f, -1f, 0f);
                    }
                    break;
            }
        }


        // Robot joints must be setup before setting joint values
        public void SetupRobotJoint()
        {
            if (!independent)
            {
                if (mimicParent == null)
                {
                    Debug.LogError(name + ": " + "Mimic parent not set");
                }
                else
                {
                    if (!mimicParent.independent)
                    {
                        Debug.LogError(name + ": " + "Mimic parent is not independent joint");
                    }
                    else
                    {
                        mimicParent.mimicJoints.Add(this);
                    }
                }
            }
        }


        public void SetJointValueDefault()
        {
            if (independent) SetJointValue(defaultJointValue);
        }

        public void SetJointValue(float val) // added debug logs to get joint angles.
        {
            if (independent)
            {
                switch (jointType)
                {
                    case JointType.Continuous:
                        jointObject.transform.localEulerAngles = jointOperator * val;
                        currentJointValue = val;
                        //Debug.Log($"{name} Joint (Continuous): Current Value = {currentJointValue}");
                        break;
                    case JointType.Revolute:
                        if (val <= jointLimit.y + delta && val >= jointLimit.x - delta)
                        {
                            jointObject.transform.localEulerAngles = jointOperator * val;
                            currentJointValue = val;
                        }
                        else if (val > jointLimit.y + delta)
                        {
                            jointObject.transform.localEulerAngles = jointOperator * jointLimit.y;
                            currentJointValue = jointLimit.y;
                        }
                        else
                        {
                            jointObject.transform.localEulerAngles = jointOperator * jointLimit.x;
                            currentJointValue = jointLimit.x;
                        }
                        //Debug.Log($"{name} Joint (Revolute): Current Value = {currentJointValue}");
                        break;
                    case JointType.Prismatic:
                        if (val <= jointLimit.y + delta && val >= jointLimit.x - delta)
                        {
                            jointObject.transform.localPosition = jointOperator * val;
                            currentJointValue = val;
                        }
                        else if (val > jointLimit.y + delta)
                        {
                            jointObject.transform.localPosition = jointOperator * jointLimit.y;
                            currentJointValue = jointLimit.y;
                        }
                        else
                        {
                            jointObject.transform.localPosition = jointOperator * jointLimit.x;
                            currentJointValue = jointLimit.x;
                        }
                        //Debug.Log($"{name} Joint (Prismatic): Current Value = {currentJointValue}");
                        break;
                    case JointType.Fixed:
                        return;
                }
                // Send the joint value to Python through UDPClient
                if (udpClient != null)
                {
                    string message = $"{name},{currentJointValue}";
                    //udpClient.SendJointData(message);
                }
                foreach (URDFJoint j in mimicJoints)
                {
                    j.SetJointValue(currentJointValue);
                }
            }
            // mimic joints
            else
            {
                val = val * mimicFactor;
                switch (jointType)
                {
                    case JointType.Continuous:
                        jointObject.transform.localEulerAngles = jointOperator * val;
                        currentJointValue = val;
                        //Debug.Log($"{name} Mimic Joint (Continuous): Current Value = {currentJointValue}");
                        break;
                    case JointType.Revolute:
                        if (val <= jointLimit.y + delta && val >= jointLimit.x - delta)
                        {
                            jointObject.transform.localEulerAngles = jointOperator * val;
                            currentJointValue = val;
                        }
                        else if (val > jointLimit.y + delta)
                        {
                            jointObject.transform.localEulerAngles = jointOperator * jointLimit.y;
                            currentJointValue = jointLimit.y;
                        }
                        else
                        {
                            jointObject.transform.localEulerAngles = jointOperator * jointLimit.x;
                            currentJointValue = jointLimit.x;
                        }
                        //Debug.Log($"{name} Mimic Joint (Revolute): Current Value = {currentJointValue}");
                        break;
                    case JointType.Prismatic:
                        if (val <= jointLimit.y + delta && val >= jointLimit.x - delta)
                        {
                            jointObject.transform.localPosition = jointOperator * val;
                            currentJointValue = val;
                        }
                        else if (val > jointLimit.y + delta)
                        {
                            jointObject.transform.localPosition = jointOperator * jointLimit.y;
                            currentJointValue = jointLimit.y;
                        }
                        else
                        {
                            jointObject.transform.localPosition = jointOperator * jointLimit.x;
                            currentJointValue = jointLimit.x;
                        }
                        //Debug.Log($"{name} Mimic Joint (Prismatic): Current Value = {currentJointValue}");
                        break;
                    case JointType.Fixed:
                        return;
                }
            }
        }

    }


}
