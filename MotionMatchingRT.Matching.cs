using System.Diagnostics;
using AnimationSystem.Math.Burst;
using OfficeOpenXml.FormulaParsing.Excel.Functions.DateTime;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AnimationSystem.AnimGraph
{
    internal partial class MotionMatchingRT
    {
        private void ScheduleMatchingJob()
        {
            m_IsJobRunning = true;
            int matchSegmentID = 0;//graphRT.GetParameter(m_NodeConfig.matchSegmentID);

            MatchingJob enterJob = new MatchingJob()
            {
                matchSegmentID = matchSegmentID,

                categories = m_Database.nativeCategories,
                features = m_Database.nativeFeatures,
                segments = m_Database.nativeSegments,
                scales = m_Database.nativeScales,

                queryFeature = m_Manager.queryFeature,
                categoryID = m_Manager.matchingCategory,
                lastCategoryID = m_Manager.lastCategory,
                tagMask = m_Manager.nextMatchTagMask,
                currentFrame = m_CurrentFrame,
                lastSegmentIndex = m_LastSegmentIndex,
                trajectoryWeight = m_Manager.responsiveness
            };


            BVHMatchingJob enterBVHJob = new BVHMatchingJob()
            {
                matchSegmentID = matchSegmentID,

                categories = m_Database.nativeCategories,
                features = m_Database.nativeFeatures,
                segments = m_Database.nativeSegments,
                scales = m_Database.nativeScales,

                queryFeature = m_Manager.queryFeature,
                categoryID = m_Manager.matchingCategory,
                lastCategoryID = m_Manager.lastCategory,
                tagMask = m_Manager.nextMatchTagMask,
                currentFrame = m_CurrentFrame,
                lastSegmentIndex = m_LastSegmentIndex,
                trajectoryWeight = m_Manager.responsiveness,

                LargeBVHSize = m_Database.LargeBVHSize,
                SmallBVHSize = m_Database.SmallBVHSize,
                FeatureSize = m_Database.FeatureSize,

                largeBoundingBoxMax = m_Database.LargeBoundingBoxMax,
                largeBoundingBoxMin = m_Database.LargeBoundingBoxMin,
                smallBoundingBoxMax = m_Database.SmallBoundingBoxMax,
                smallBoundingBoxMin = m_Database.SmallBoundingBoxMin
            };

            //m_MotionMatchingJobHandle = enterJob.Schedule();
            m_MotionMatchingJobHandle = enterBVHJob.Schedule();
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct MatchingJob : IJob
        {
            public int matchSegmentID;
            public NativeArray<MotionMatchingDatabase.Category> categories;
            public NativeArray<MotionMatchingDatabase.Segment> segments;
            public NativeArray<float> features;
            public NativeArray<float> scales;
            public GraphRT.MotionMatchingManager.QueryFeature queryFeature;
            public int categoryID;
            public int tagMask;
            public int lastSegmentIndex;
            public int lastCategoryID;
            public int currentFrame;
            public float trajectoryWeight;
            private float m_BiasWeight;

            private float m_BestCost;
            private int m_BestFrame;
            private int m_BestSegmentIndex;

            private int dimension => queryFeature.dimension;

            public void Execute()
            {
                ConstructQueryFeature();

                int dimension4 = dimension / 4;
                int dimensionTail = dimension - dimension4 * 4;
                int dimensionAlign4 = dimension4 + (dimensionTail > 0 ? 1 : 0);

                NativeArray<float4> goal = new NativeArray<float4>(dimensionAlign4, Allocator.Temp);
                NativeArray<float4> weights = new NativeArray<float4>(dimensionAlign4, Allocator.Temp);
                {
                    int dim1 = 0;
                    int dim4 = 0;
                    for (; dim4 < dimension4; dim4++, dim1 += 4)
                    {
                        goal[dim4] = new float4(queryFeature.data[dim1], queryFeature.data[dim1 + 1], queryFeature.data[dim1 + 2], queryFeature.data[dim1 + 3]);
                        weights[dim4] = new float4(queryFeature.weights[dim1], queryFeature.weights[dim1 + 1], queryFeature.weights[dim1 + 2], queryFeature.weights[dim1 + 3]);
                    }

                    if (dimensionTail > 0)
                    {
                        float4 featuresTail = float4.zero;
                        float4 weightsTail = float4.zero;
                        for (int dimTail = 0; dim1 < dimension; dim1++, dimTail++)
                        {
                            featuresTail[dimTail] = queryFeature.data[dim1];
                            weightsTail[dimTail] = queryFeature.weights[dim1];
                        }

                        goal[dim4] = featuresTail;
                        weights[dim4] = weightsTail;
                    }
                }

                if (matchSegmentID != 0)
                {

                }
                else
                {
                    for (int categoryIndex = 0; categoryIndex < categories.Length; categoryIndex++)
                    {
                        var category = categories[categoryIndex];
                        if (category.id == categoryID)
                        {
                            int segment0 = (int)category.beginSegment;
                            int segment1 = (int)category.endSegment;

                            for (int segmentIndex = segment0; segmentIndex < segment1; segmentIndex++)
                            {
                                MotionMatchingDatabase.Segment segment = segments[segmentIndex];
                                if ((segment.tag & tagMask) != 0)
                                {
                                    int segmentBegin = segment.beginFrame;
                                    int segmentEnd = segment.endFrame;

                                    for (int frameIndex = segmentBegin; frameIndex < segmentEnd; frameIndex++)
                                    {
                                        float4 cost4 = float4.zero;
                                        int featureIndex = frameIndex * dimension;

                                        int dim1 = 0;
                                        int dim4 = 0;
                                        for (; dim4 < dimension4; dim4++, dim1 += 4)
                                        {
                                            float4 v = new float4(features[featureIndex + dim1],
                                                features[featureIndex + dim1 + 1],
                                                features[featureIndex + dim1 + 2], features[featureIndex + dim1 + 3]);
                                            cost4 += Missing.squared(v - goal[dim4]) * weights[dim4];
                                            if (math.csum(cost4) > m_BestCost)
                                            {
                                                break;
                                            }
                                        }

                                        if (math.csum(cost4) > m_BestCost)
                                        {
                                            continue;
                                        }


                                        // tail
                                        float4 tail = float4.zero;
                                        for (int dimTail = 0; dim1 < dimension; dim1++, dimTail++)
                                        {
                                            tail[dimTail] = features[featureIndex + dim1];
                                        }

                                        cost4 += Missing.squared(tail - goal[dim4]) * weights[dim4];

                                        // cost4 to cost1
                                        float cost = math.csum(cost4);
                                        if (cost < m_BestCost)
                                        {
                                            m_BestCost = cost;
                                            m_BestFrame = frameIndex;
                                            m_BestSegmentIndex = segmentIndex;
                                        }
                                    }
                                }
                            }

                        }
                    }
                }

                // BiasToTheSameFrame
                if (currentFrame >= 0 && matchSegmentID == 0)
                {
                    // MotionMatchingDatabase.Segment segment = segments[lastSegmentIndex];
                    // if ((segment.tag & tagMask) != 0 && lastCategoryID == categoryID)
                    if (lastCategoryID == categoryID)
                    {
                        int poseFeatureBegin = currentFrame * dimension;

                        // only trajectory cost, treat next pose as the best pose, whose pose cost is zero
                        float cost = 0.0f;
                        for (int featureIndex = 0; featureIndex < 12; featureIndex++)
                        {
                            float distance = queryFeature.data[featureIndex] -
                                             features[poseFeatureBegin + featureIndex];
                            cost += distance * distance * queryFeature.weights[featureIndex];
                        }

                        if (cost * m_BiasWeight < m_BestCost)
                        {
                            m_BestCost = cost;
                            m_BestFrame = currentFrame;
                            m_BestSegmentIndex = lastSegmentIndex;
                        }
                    }
                }

                weights.Dispose();
                goal.Dispose();

                if (m_BestFrame >= 0)
                {
                    MotionMatchingDatabase.Segment bestSegment = segments[m_BestSegmentIndex];
                    if (m_BestFrame == bestSegment.endFrame - 1)
                    {
                        m_BestSegmentIndex = bestSegment.nextIndex;
                        bestSegment = segments[m_BestSegmentIndex];
                        m_BestFrame = bestSegment.beginFrame;
                    }

                    var info = queryFeature.info;
                    info[0] = m_BestFrame;
                    info[1] = m_BestSegmentIndex;
                }
            }

            private void ConstructQueryFeature()
            {
                m_BestFrame = -1;
                m_BestCost = float.MaxValue;
                m_BestSegmentIndex = -1;

                // fill in responsiveness
                for (int i = 0; i < 12; i++)
                {
                    queryFeature.weights[i] = scales[i] * scales[i] * trajectoryWeight;
                }
                float poseWeight = 1f - trajectoryWeight;
                for (int i = 12; i < scales.Length; i++)
                {
                    queryFeature.weights[i] = scales[i] * scales[i] * poseWeight;
                }

                if (currentFrame >= 0)
                {
                    currentFrame += segments[lastSegmentIndex].beginFrame;
                    int poseFeatureBegin = currentFrame * dimension;
                    for (int featureIndex = 12; featureIndex < dimension; featureIndex++)
                    {
                        queryFeature.data[featureIndex] = features[poseFeatureBegin + featureIndex];
                    }

                    m_BiasWeight = segments[lastSegmentIndex].biasWeight;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct BVHMatchingJob : IJob
        {
            public int matchSegmentID;
            public NativeArray<MotionMatchingDatabase.Category> categories;
            public NativeArray<MotionMatchingDatabase.Segment> segments;
            public NativeArray<float> features;
            public NativeArray<float> scales;
            public GraphRT.MotionMatchingManager.QueryFeature queryFeature;
            public int categoryID;
            public int tagMask;
            public int lastSegmentIndex;
            public int lastCategoryID;
            public int currentFrame;
            public float trajectoryWeight;
            private float m_BiasWeight;

            private float m_BestCost;
            private int m_BestFrame;
            private int m_BestSegmentIndex;

            private int dimension => queryFeature.dimension;
            public float CurrentDistance;

            public NativeArray<float> largeBoundingBoxMin;
            public NativeArray<float> largeBoundingBoxMax;
            public NativeArray<float> smallBoundingBoxMin;
            public NativeArray<float> smallBoundingBoxMax;

            public int LargeBVHSize;
            public int SmallBVHSize;
            public int FeatureSize;



            public void Execute()
            {
                ConstructQueryFeature();

                int dimension4 = dimension / 4;
                int dimensionTail = dimension - dimension4 * 4;
                int dimensionAlign4 = dimension4 + (dimensionTail > 0 ? 1 : 0);

                NativeArray<float4> goal = new NativeArray<float4>(dimensionAlign4, Allocator.Temp);
                NativeArray<float4> weights = new NativeArray<float4>(dimensionAlign4, Allocator.Temp);
                {
                    int dim1 = 0;
                    int dim4 = 0;
                    for (; dim4 < dimension4; dim4++, dim1 += 4)
                    {
                        goal[dim4] = new float4(queryFeature.data[dim1], queryFeature.data[dim1 + 1], queryFeature.data[dim1 + 2], queryFeature.data[dim1 + 3]);
                        weights[dim4] = new float4(queryFeature.weights[dim1], queryFeature.weights[dim1 + 1], queryFeature.weights[dim1 + 2], queryFeature.weights[dim1 + 3]);
                    }

                    if (dimensionTail > 0)
                    {
                        float4 featuresTail = float4.zero;
                        float4 weightsTail = float4.zero;
                        for (int dimTail = 0; dim1 < dimension; dim1++, dimTail++)
                        {
                            featuresTail[dimTail] = queryFeature.data[dim1];
                            weightsTail[dimTail] = queryFeature.weights[dim1];
                        }

                        goal[dim4] = featuresTail;
                        weights[dim4] = weightsTail;
                    }
                }

                if (matchSegmentID != 0)
                {

                }
                else
                {
                    if (currentFrame >= 0)
                    {
                        float CurrentCost = 0;
                        for (int featureIndex = 0; featureIndex < 12; featureIndex++)
                        {
                            float distance = queryFeature.data[featureIndex] -
                                             features[currentFrame * dimension + featureIndex];
                            CurrentCost += distance * distance * queryFeature.weights[featureIndex];
                        }
                        m_BestCost = CurrentCost;
                    }
                    else
                    {
                        return;
                    }

                    for (int categoryIndex = 0; categoryIndex < categories.Length; categoryIndex++)
                    {
                        var category = categories[categoryIndex];
                        if (category.id == categoryID)
                        {
                            int segment0 = (int)category.beginSegment;
                            int segment1 = (int)category.endSegment;
                            for (int segmentIndex = segment0; segmentIndex < segment1; segmentIndex++)
                            {
                                MotionMatchingDatabase.Segment segment = segments[segmentIndex];
                                if ((segment.tag & tagMask) != 0)
                                {
                                    int segmentBegin = segment.beginFrame;
                                    int segmentEnd = segment.endFrame;

                                    //TODO: AABB Accelerate
                                    //ConstructBounds(segmentBegin, segmentEnd);
                                    int i = segmentBegin;

                                    while (i < segmentEnd + 1)
                                    {
                                        // Current and next large box
                                        int iLarge = i / LargeBVHSize;
                                        int iLargeIndex = iLarge * dimension;
                                        int iLargeNext = (iLarge + 1) * LargeBVHSize;

                                        // Find distance to box
                                        float choosenCost = 0.0f;
                                        for (int j = 0; j < dimension; ++j)
                                        {
                                            float query = queryFeature.data[j];
                                            float largemin = largeBoundingBoxMin[iLargeIndex + j];
                                            float largemax = largeBoundingBoxMax[iLargeIndex + j];
                                            float cost = query - math.clamp(query, largemin, largemax);
                                            choosenCost += cost * cost * queryFeature.weights[j];
                                            if (choosenCost >= m_BestCost)
                                            {
                                                break;
                                            }
                                        }
                                        // If distance is already greater... next box
                                        if (choosenCost >= m_BestCost)
                                        {
                                            i = iLargeNext;
                                            continue;
                                        }

                                        // Search small box
                                        while (i < iLargeNext && i < segmentEnd + 1)
                                        {
                                            // Current and next small box
                                            int iSmall = i / SmallBVHSize;
                                            int iSmallIndex = iSmall * dimension;
                                            int iSmallNext = (iSmall + 1) * SmallBVHSize;

                                            // Find distance to box
                                            choosenCost = 0.0f;
                                            for (int j = 0; j < dimension; ++j)
                                            {
                                                float query = queryFeature.data[j];
                                                float cost = query - math.clamp(query, smallBoundingBoxMin[iSmallIndex + j], smallBoundingBoxMax[iSmallIndex + j]);
                                                choosenCost += cost * cost * queryFeature.weights[j];
                                                if (choosenCost >= m_BestCost)
                                                {
                                                    break;
                                                }
                                            }

                                            // If distance is already greater... next box
                                            if (choosenCost >= m_BestCost)
                                            {
                                                i = iSmallNext;
                                                continue;
                                            }

                                            //    // Search inside small box
                                            while (i < iSmallNext && i < segmentEnd + 1)
                                            {
                                                // Test all frames

                                                float4 cost4 = float4.zero;
                                                int featureIndex = i * dimension;

                                                int dim1 = 0;
                                                int dim4 = 0;
                                                for (; dim4 < dimension4; dim4++, dim1 += 4)
                                                {
                                                    float4 v = new float4(features[featureIndex + dim1],
                                                        features[featureIndex + dim1 + 1],
                                                        features[featureIndex + dim1 + 2], features[featureIndex + dim1 + 3]);
                                                    cost4 += Missing.squared(v - goal[dim4]) * weights[dim4];
                                                    if (math.csum(cost4) > m_BestCost)
                                                    {
                                                        break;
                                                    }
                                                }

                                                if (math.csum(cost4) > m_BestCost)
                                                {
                                                    i++;
                                                    continue;
                                                }

                                                // tail
                                                float4 tail = float4.zero;
                                                for (int dimTail = 0; dim1 < dimension; dim1++, dimTail++)
                                                {
                                                    tail[dimTail] = features[featureIndex + dim1];
                                                }

                                                cost4 += Missing.squared(tail - goal[dim4]) * weights[dim4];

                                                // cost4 to cost1
                                                float cost1 = math.csum(cost4);

                                                // If cost is lower than best... update
                                                if (cost1 < m_BestCost)
                                                {
                                                    m_BestCost = cost1;
                                                    m_BestFrame = i;
                                                    m_BestSegmentIndex = segmentIndex;
                                                }

                                                i++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                // BiasToTheSameFrame
                if (currentFrame >= 0 && matchSegmentID == 0)
                {
                    // MotionMatchingDatabase.Segment segment = segments[lastSegmentIndex];
                    // if ((segment.tag & tagMask) != 0 && lastCategoryID == categoryID)
                    if (lastCategoryID == categoryID)
                    {
                        int poseFeatureBegin = currentFrame * dimension;

                        // only trajectory cost, treat next pose as the best pose, whose pose cost is zero
                        float cost = 0.0f;
                        for (int featureIndex = 0; featureIndex < 12; featureIndex++)
                        {
                            float distance = queryFeature.data[featureIndex] -
                                             features[poseFeatureBegin + featureIndex];
                            cost += distance * distance * queryFeature.weights[featureIndex];
                        }

                        if (cost * m_BiasWeight < m_BestCost)
                        {
                            m_BestCost = cost;
                            m_BestFrame = currentFrame;
                            m_BestSegmentIndex = lastSegmentIndex;
                        }
                    }
                }

                weights.Dispose();
                goal.Dispose();

                if (m_BestFrame >= 0)
                {
                    MotionMatchingDatabase.Segment bestSegment = segments[m_BestSegmentIndex];
                    if (m_BestFrame == bestSegment.endFrame - 1)
                    {
                        m_BestSegmentIndex = bestSegment.nextIndex;
                        bestSegment = segments[m_BestSegmentIndex];
                        m_BestFrame = bestSegment.beginFrame;
                    }

                    var info = queryFeature.info;
                    info[0] = m_BestFrame;
                    info[1] = m_BestSegmentIndex;
                }

            }

            private void ConstructQueryFeature()
            {
                m_BestFrame = -1;
                m_BestCost = float.MaxValue;
                m_BestSegmentIndex = -1;

                // fill in responsiveness
                for (int i = 0; i < 12; i++)
                {
                    queryFeature.weights[i] = scales[i] * scales[i] * trajectoryWeight;
                }
                float poseWeight = 1f - trajectoryWeight;
                for (int i = 12; i < scales.Length; i++)
                {
                    queryFeature.weights[i] = scales[i] * scales[i] * poseWeight;
                }

                if (currentFrame >= 0)
                {
                    currentFrame += segments[lastSegmentIndex].beginFrame;
                    int poseFeatureBegin = currentFrame * dimension;
                    for (int featureIndex = 12; featureIndex < dimension; featureIndex++)
                    {
                        queryFeature.data[featureIndex] = features[poseFeatureBegin + featureIndex];
                    }

                    m_BiasWeight = segments[lastSegmentIndex].biasWeight;
                }
            }
        }
    }
}
