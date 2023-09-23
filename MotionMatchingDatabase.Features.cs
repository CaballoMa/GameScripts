using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml.Spreadsheet;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AnimationSystem.AnimGraph
{
    public partial class MotionMatchingDatabase
    {
        [Serializable]
        public struct Category
        {
            public int id;
            public short beginSegment;
            public short endSegment;
        }

        [Serializable]
        public struct Segment
        {
            public int beginFrame;
            public int endFrame;
            public int tag;
            public int nextIndex;
            public float biasWeight;
        }

        // Do not add [Flags]
        public enum EMatchTag
        {
            Any = -1,
            None = 0,
            Turn = 1 << 0,
            Start = 1 << 1,
            Stop = 1 << 2,
            Loop = 1 << 3,
            Idle = 1 << 4,
            Transition = 1 << 5,
            Reposition = 1 << 6
        }

        [Serializable]
        public class Animation
        {
            public UnityEngine.AnimationClip clip;
        }

        [SerializeField, NonReorderable] public Animation[] animations;
        [SerializeField, NonReorderable] public int2[] nameToSegmentIndex;
        //TODO: Write to binary file
        [SerializeField, NonReorderable] public Category[] categories;
        [SerializeField, NonReorderable] public Segment[] segments;
        [SerializeField, NonReorderable] public float[] features;
        [SerializeField, NonReorderable] public float[] scales;

        [NonSerialized] public NativeArray<Category> nativeCategories;
        [NonSerialized] public NativeArray<Segment> nativeSegments;
        [NonSerialized] public NativeArray<float> nativeFeatures;
        [NonSerialized] public NativeArray<float> nativeScales;

        [NonSerialized] public NativeArray<float> LargeBoundingBoxMin;
        [NonSerialized] public NativeArray<float> LargeBoundingBoxMax;
        [NonSerialized] public NativeArray<float> SmallBoundingBoxMin;
        [NonSerialized] public NativeArray<float> SmallBoundingBoxMax;

        [NonSerialized] public int LargeBVHSize;
        [NonSerialized] public int SmallBVHSize;
        [NonSerialized] public int FeatureSize;
        [NonSerialized] public int numberBoundingBoxSmall;
        [NonSerialized] public int numberBoundingBoxLarge;

        [NonSerialized] public int referenceCount = 0;
        [NonSerialized] private bool m_IsValid = false;

        public void ReleaseMemory()
        {
            referenceCount = 0;
            if (m_IsValid)
            {
                m_IsValid = false;
                nativeCategories.Dispose();
                nativeSegments.Dispose();
                nativeFeatures.Dispose();
                nativeScales.Dispose();

                LargeBoundingBoxMin.Dispose();
                LargeBoundingBoxMax.Dispose();
                SmallBoundingBoxMin.Dispose();
                SmallBoundingBoxMax.Dispose();
            }
        }

        public void Allocate()
        {
            if (m_IsValid)
            {
                referenceCount++;
                return;
            }

            nativeCategories = new NativeArray<Category>(categories, Allocator.Persistent);
            nativeSegments = new NativeArray<Segment>(segments, Allocator.Persistent);
            nativeFeatures = new NativeArray<float>(features, Allocator.Persistent);
            nativeScales = new NativeArray<float>(scales, Allocator.Persistent);

            LargeBVHSize = 64;
            SmallBVHSize = 8;
            FeatureSize = 33;
            numberBoundingBoxLarge = features.Length / (FeatureSize * LargeBVHSize);
            numberBoundingBoxSmall = features.Length / (FeatureSize * SmallBVHSize);

            LargeBoundingBoxMax = new NativeArray<float>((numberBoundingBoxLarge + 1) * FeatureSize, Allocator.Persistent);
            LargeBoundingBoxMin = new NativeArray<float>((numberBoundingBoxLarge + 1) * FeatureSize, Allocator.Persistent);
            SmallBoundingBoxMax = new NativeArray<float>((numberBoundingBoxSmall + 1) * FeatureSize, Allocator.Persistent);
            SmallBoundingBoxMin = new NativeArray<float>((numberBoundingBoxSmall + 1) * FeatureSize, Allocator.Persistent);

            ConstructBounds();

            referenceCount = 1;
            m_IsValid = true;
        }

        private void ConstructBounds()
        {
            // Initialize Bounding Box
            for (int i = 0; i < LargeBoundingBoxMin.Length; i++) LargeBoundingBoxMin[i] = float.MaxValue;
            for (int i = 0; i < LargeBoundingBoxMax.Length; i++) LargeBoundingBoxMax[i] = float.MinValue;
            for (int i = 0; i < SmallBoundingBoxMin.Length; i++) SmallBoundingBoxMin[i] = float.MaxValue;
            for (int i = 0; i < SmallBoundingBoxMax.Length; i++) SmallBoundingBoxMax[i] = float.MinValue;

            int startFrame = segments[0].beginFrame;
            int endFrame = segments[segments.Length - 1].endFrame;

            for (int i = startFrame; i < endFrame; ++i)
            {
                int iSmallIndex = (i / SmallBVHSize) * FeatureSize;
                int iLargeIndex = (i / LargeBVHSize) * FeatureSize;

                for (int j = 0; j < FeatureSize; ++j)
                {
                    float feature = features[i * FeatureSize + j];
                    LargeBoundingBoxMin[iLargeIndex + j] = math.min(LargeBoundingBoxMin[iLargeIndex + j], feature);
                    LargeBoundingBoxMax[iLargeIndex + j] = math.max(LargeBoundingBoxMax[iLargeIndex + j], feature);
                    SmallBoundingBoxMin[iSmallIndex + j] = math.min(SmallBoundingBoxMin[iSmallIndex + j], feature);
                    SmallBoundingBoxMax[iSmallIndex + j] = math.max(SmallBoundingBoxMax[iSmallIndex + j], feature);
                }
            }
        }

        private void OnEnable()
        {
            ReleaseMemory();
        }

        private void OnDestroy()
        {
            ReleaseMemory();
        }

        public bool IsActive()
        {
            return m_IsValid;
        }
    }
}
