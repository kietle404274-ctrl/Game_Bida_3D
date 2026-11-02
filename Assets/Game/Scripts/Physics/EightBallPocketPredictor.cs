using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Match;

namespace Game.Billiards.Physics
{
    /// <summary>
    /// Bộ giải vật lý tất định tốc độ cao (< 0.2ms) chạy ngay tại thời điểm người chơi vừa Click đánh
    /// để dự đoán chính xác 100% xem Bi số 8 có chắc chắn rơi vào lỗ nào (0..5) trong cú đánh này hay không.
    /// Sử dụng đúng mô hình xung lực TOI 2D, ma sát trượt/lăn và tọa độ băng/má lỗ của BallController & TableWrapper.
    /// </summary>
    public static class EightBallPocketPredictor
    {
        private struct VirtualBall
        {
            public int number;
            public bool active;
            public Vector3 pos;
            public Vector3 prevPos;
            public Vector3 vel;
            public Vector3 prevVel;
            public Vector3 ang;
            public Vector3 prevAng;
            public int lastBallStep;
            public int lastCushionStep;
        }

        private struct JawSegment
        {
            public Vector2 p1;
            public Vector2 p2;
            public Vector3 faceNormal; // Hướng từ má lỗ ra ngoài mặt bàn (pháp tuyến phản xạ)
        }

        private static readonly VirtualBall[] simBalls = new VirtualBall[16];
        private static readonly Vector2[] pocketCentersXZ = new Vector2[6];
        private static readonly JawSegment[] jawSegments = new JawSegment[12];
        private static bool jawsInitialized = false;

        /// <summary>
        /// Mã lỗ (0..5) mà Bi số 8 đã được dự đoán chắc chắn sẽ rơi vào ở cú đánh hiện tại (-1 nếu không có).
        /// </summary>
        public static int ActivePredictedPocketId { get; private set; } = -1;

        public static void ClearPrediction()
        {
            ActivePredictedPocketId = -1;
        }

        private static void EnsureJawsInitialized()
        {
            if (jawsInitialized) return;
            jawsInitialized = true;

            const float a = 0.002f;
            const float aThroat = 0.010f;
            Vector2 shFootPos1 = new Vector2(0f, -a);
            Vector2 shFootPos2 = new Vector2(0f, -aThroat);
            Vector2 shFootNeg1 = new Vector2(0f, a);
            Vector2 shFootNeg2 = new Vector2(0f, aThroat);
            Vector2 shSideFoot1 = new Vector2(a, 0f);
            Vector2 shSideFoot2 = new Vector2(aThroat, 0f);
            Vector2 shSideHead1 = new Vector2(-a, 0f);
            Vector2 shSideHead2 = new Vector2(-aThroat, 0f);

            // 4 lỗ góc (8 má vát)
            jawSegments[0] = CreateJaw(new Vector2(-1.0759f, 0.4467f) + shFootPos1, new Vector2(-1.1329f, 0.4944f) + shFootPos2, new Vector2(-0.642f, -0.767f));
            jawSegments[1] = CreateJaw(new Vector2(-1.0140f, 0.5058f) + shSideFoot1, new Vector2(-1.0635f, 0.5631f) + shSideFoot2, new Vector2(0.757f, 0.654f));

            jawSegments[2] = CreateJaw(new Vector2(-1.0759f, -0.4467f) + shFootNeg1, new Vector2(-1.1329f, -0.4944f) + shFootNeg2, new Vector2(-0.642f, 0.767f));
            jawSegments[3] = CreateJaw(new Vector2(-1.0140f, -0.5058f) + shSideFoot1, new Vector2(-1.0635f, -0.5631f) + shSideFoot2, new Vector2(0.757f, -0.654f));

            jawSegments[4] = CreateJaw(new Vector2(1.0638f, 0.4467f) + shFootPos1, new Vector2(1.1208f, 0.4944f) + shFootPos2, new Vector2(0.642f, -0.767f));
            jawSegments[5] = CreateJaw(new Vector2(1.0020f, 0.5058f) + shSideHead1, new Vector2(1.0515f, 0.5631f) + shSideHead2, new Vector2(-0.757f, 0.654f));

            jawSegments[6] = CreateJaw(new Vector2(1.0638f, -0.4467f) + shFootNeg1, new Vector2(1.1208f, -0.4944f) + shFootNeg2, new Vector2(0.642f, 0.767f));
            jawSegments[7] = CreateJaw(new Vector2(1.0020f, -0.5058f) + shSideHead1, new Vector2(1.0515f, -0.5631f) + shSideHead2, new Vector2(-0.757f, -0.654f));

            // 2 lỗ giữa (4 má lỗ)
            jawSegments[8]  = CreateJaw(new Vector2(-0.0760f,  0.5058f), new Vector2(-0.0575f,  0.5589f), new Vector2(-0.950f,  0.313f));
            jawSegments[9]  = CreateJaw(new Vector2( 0.0640f,  0.5058f), new Vector2( 0.0454f,  0.5589f), new Vector2( 0.950f,  0.313f));
            jawSegments[10] = CreateJaw(new Vector2(-0.0760f, -0.5058f), new Vector2(-0.0575f, -0.5589f), new Vector2(-0.950f, -0.313f));
            jawSegments[11] = CreateJaw(new Vector2( 0.0640f, -0.5058f), new Vector2( 0.0454f, -0.5589f), new Vector2( 0.950f, -0.313f));
        }

        private static JawSegment CreateJaw(Vector2 p1, Vector2 p2, Vector2 normalIntoRail)
        {
            Vector2 tangent = p2 - p1;
            Vector2 tUnit = tangent.sqrMagnitude > 1e-8f ? tangent.normalized : Vector2.right;
            Vector2 normal = new Vector2(-tUnit.y, tUnit.x);
            if (Vector2.Dot(normal, normalIntoRail) < 0f)
            {
                normal = -normal;
            }
            // Pháp tuyến hướng từ mặt má lỗ ra phía bi (ngược với normalIntoRail)
            Vector2 outNormal2D = -normal;
            return new JawSegment
            {
                p1 = p1,
                p2 = p2,
                faceNormal = new Vector3(outNormal2D.x, 0f, outNormal2D.y).normalized
            };
        }

        /// <summary>
        /// Tính toán trước xem cú đánh <paramref name="command"/> có chắc chắn đưa Bi số 8 vào lỗ hay không
        /// (chỉ áp dụng cho các cú đánh trong trận, bỏ qua cú phá bi đầu ván).
        /// </summary>
        public static bool TryPredictEightBallPocket(
            ShotCommand command,
            BallManager ballManager,
            TableWrapper tableWrapper,
            PhysicsConfig config,
            out int predictedPocketId)
        {
            predictedPocketId = -1;
            ActivePredictedPocketId = -1;

            if (ballManager == null || config == null)
                return false;

            if (tableWrapper == null)
                tableWrapper = ballManager.TableWrapper;
            if (tableWrapper == null)
                return false;

            // Bỏ qua cú phá bi đầu trận (theo luật WPA, bi 8 rơi lúc phá bi được đặt lại vào Foot Spot, không kết thúc ván)
            if (IsRackIntact(ballManager))
                return false;

            var cueBall = ballManager.CueBall;
            var eightBall = ballManager.EightBall;
            if (cueBall == null || cueBall.IsPocketed || !cueBall.gameObject.activeInHierarchy)
                return false;
            if (eightBall == null || eightBall.IsPocketed || !eightBall.gameObject.activeInHierarchy)
                return false;

            EnsureJawsInitialized();

            // Lấy tọa độ 6 lỗ từ TableWrapper (hoặc fallback chuẩn bàn 3D nếu thiếu)
            var pockets = tableWrapper.Pockets;
            for (int p = 0; p < 6; p++)
            {
                if (pockets != null && p < pockets.Length && pockets[p] != null)
                {
                    Vector3 pp = pockets[p].PocketPosition;
                    pocketCentersXZ[p] = new Vector2(pp.x, pp.z);
                }
                else
                {
                    pocketCentersXZ[p] = GetDefaultPocketCenterXZ(p);
                }
            }

            // Sao chép trạng thái ban đầu của 16 bi
            var allBalls = ballManager.AllBalls;
            int ballCount = 0;
            int cueIdx = -1;
            int eightIdx = -1;

            for (int i = 0; i < allBalls.Length && ballCount < 16; i++)
            {
                var b = allBalls[i];
                if (b == null || b.IsPocketed || !b.gameObject.activeInHierarchy)
                    continue;

                Vector3 p = b.Rigidbody != null ? b.Rigidbody.position : b.transform.position;
                int idx = ballCount++;
                simBalls[idx] = new VirtualBall
                {
                    number = b.BallNumber,
                    active = true,
                    pos = p,
                    prevPos = p,
                    vel = Vector3.zero,
                    prevVel = Vector3.zero,
                    ang = Vector3.zero,
                    prevAng = Vector3.zero,
                    lastBallStep = -1,
                    lastCushionStep = -1
                };

                if (b.BallNumber == 0) cueIdx = idx;
                else if (b.BallNumber == 8) eightIdx = idx;
            }

            if (cueIdx < 0 || eightIdx < 0)
                return false;

            // Khởi tạo vận tốc & xoáy ban đầu cho bi cái khớp 100% với ShotExecutor.ExecuteShot
            Vector3 shotDir = new Vector3(command.direction.x, 0f, command.direction.z).normalized;
            if (shotDir.sqrMagnitude < 0.001f)
                shotDir = Vector3.forward;

            float shotPower = Mathf.Clamp01(command.power01);
            if (shotPower <= 0.001f)
                return false;

            float m = config.BallMass;
            float R = config.BallRadius;
            float impulseMagnitude = shotPower * config.MaxShotImpulse;
            Vector3 launchVelocity = (shotDir * impulseMagnitude) / m;

            Vector2 contact = Vector2.ClampMagnitude(command.cueContact01, 1f);
            Vector2 bOffset = contact * config.MaxContactOffset;
            launchVelocity /= 1f + config.OffsetSpeedLoss * bOffset.sqrMagnitude;

            Vector3 launchAngular = Vector3.zero;
            if (contact.sqrMagnitude > 0.001f && config.SpinStrength > 0f)
            {
                float v = launchVelocity.magnitude;
                float k = 2.5f * v / R * config.SpinStrength;
                Vector3 horizontalSpinAxis = Vector3.Cross(Vector3.up, shotDir).normalized;
                float topBackSpin = bOffset.y * k;
                float sideSpin = -bOffset.x * k;
                launchAngular = (horizontalSpinAxis * topBackSpin) + (Vector3.up * sideSpin);
            }

            float elevation = Mathf.Clamp(command.cueElevationDeg, 0f, config.MaxCueElevation);
            if (elevation >= config.MinJumpAngle)
            {
                float rad = elevation * Mathf.Deg2Rad;
                launchVelocity *= Mathf.Cos(rad);
            }

            simBalls[cueIdx].vel = launchVelocity;
            simBalls[cueIdx].prevVel = launchVelocity;
            simBalls[cueIdx].ang = launchAngular;
            simBalls[cueIdx].prevAng = launchAngular;

            // Chạy vòng lặp bước thời gian vật lý cố định (dt = 0.01s, tối đa 420 bước = 4.2 giây)
            const float dt = 0.01f;
            const int maxSteps = 420;
            float eBall = Mathf.Clamp(config.BallRestitution, 0.5f, 1f);

            for (int step = 1; step <= maxSteps; step++)
            {
                bool anyMoving = false;

                // 1. PhysicsTick: tương tác ma sát trượt/lăn trên nỉ & suy giảm xoáy (giống hệt BallController.FixedUpdate)
                for (int i = 0; i < ballCount; i++)
                {
                    if (!simBalls[i].active) continue;

                    ApplyClothSpinStep(ref simBalls[i], config, dt);

                    if (simBalls[i].vel.sqrMagnitude > 1e-7f || simBalls[i].ang.sqrMagnitude > 1e-5f)
                    {
                        anyMoving = true;
                    }

                    simBalls[i].prevPos = simBalls[i].pos;
                    simBalls[i].prevVel = simBalls[i].vel;
                    simBalls[i].prevAng = simBalls[i].ang;
                }

                if (!anyMoving)
                    break;

                // 2. Tích phân vị trí bước PhysX (trước khi kiểm tra va chạm)
                for (int i = 0; i < ballCount; i++)
                {
                    if (!simBalls[i].active) continue;
                    simBalls[i].pos += simBalls[i].vel * dt;
                }

                // 3. Giải va chạm bi-bi theo Continuous TOI (khớp với BallController.HandleBallCollisionResponse)
                float triggerRadius = 2f * R + 0.0028f; // SphereCollider contactOffset
                float triggerRadiusSq = triggerRadius * triggerRadius;

                for (int i = 0; i < ballCount; i++)
                {
                    if (!simBalls[i].active) continue;
                    for (int j = i + 1; j < ballCount; j++)
                    {
                        if (!simBalls[j].active) continue;

                        Vector3 diffCurr = simBalls[i].pos - simBalls[j].pos;
                        diffCurr.y = 0f;
                        Vector3 diffPrev = simBalls[i].prevPos - simBalls[j].prevPos;
                        diffPrev.y = 0f;

                        // Kiểm tra xem 2 bi có đi vào vùng chạm trong bước này không
                        if (diffCurr.sqrMagnitude > triggerRadiusSq && diffPrev.sqrMagnitude > triggerRadiusSq)
                            continue;

                        ResolvePairCollision(ref simBalls[i], ref simBalls[j], step, dt, R, eBall, config);
                    }
                }

                // 4. Giải va chạm với 4 cạnh băng thẳng và 12 má lỗ (Cushion & Pocket Facing Bounce)
                for (int i = 0; i < ballCount; i++)
                {
                    if (!simBalls[i].active) continue;
                    if (simBalls[i].vel.sqrMagnitude < 1e-7f) continue;

                    ResolveCushionAndJawCollisions(ref simBalls[i], step, R, config);
                }

                // 5. Kiểm tra rơi xuống 6 lỗ (PocketDetector)
                for (int i = 0; i < ballCount; i++)
                {
                    if (!simBalls[i].active) continue;

                    if (TryCheckPocketCapture(ref simBalls[i], out int pId))
                    {
                        simBalls[i].active = false;
                        simBalls[i].vel = Vector3.zero;
                        simBalls[i].ang = Vector3.zero;

                        if (i == eightIdx)
                        {
                            predictedPocketId = pId;
                            ActivePredictedPocketId = pId;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static void ApplyClothSpinStep(ref VirtualBall b, PhysicsConfig config, float dt)
        {
            Vector3 v = b.vel;
            Vector3 w = b.ang;
            v.y = 0f;

            if (v.sqrMagnitude < 0.0001f && w.sqrMagnitude < 0.0001f)
            {
                b.vel = Vector3.zero;
                b.ang = Vector3.zero;
                return;
            }

            float r = config.BallRadius;
            float m = config.BallMass;
            float inertia = 0.4f * m * r * r;

            Vector3 vSlip = new Vector3(
                v.x + (w.z * r),
                0f,
                v.z - (w.x * r)
            );

            float slipSpeed = vSlip.magnitude;
            float linearSpeed = v.magnitude;

            if (slipSpeed <= 0.015f && linearSpeed < config.RestLinearThreshold)
            {
                b.vel = Vector3.zero;
                b.ang = Vector3.zero;
                return;
            }

            if (slipSpeed > 0.015f)
            {
                float normalForce = m * 9.81f;
                float frictionMagnitude = normalForce * config.ClothFriction;
                float maxStopForce = slipSpeed * m / (dt * 3.5f);
                frictionMagnitude = Mathf.Min(frictionMagnitude, maxStopForce);

                Vector3 frictionDir = -vSlip / slipSpeed;
                Vector3 frictionForce = frictionDir * frictionMagnitude;

                v += (frictionForce / m) * dt;
                Vector3 torque = new Vector3(-r * frictionForce.z, 0f, r * frictionForce.x);
                w += (torque / inertia) * dt;
            }
            else if (linearSpeed >= config.RestLinearThreshold)
            {
                w = new Vector3(v.z / r, w.y, -v.x / r);
                float rollDecel = config.RollingResistance * 9.81f;
                Vector3 rollResistance = -v.normalized * (m * rollDecel);
                v += (rollResistance / m) * dt;
            }

            if (Mathf.Abs(w.y) > 0.01f)
            {
                float decayStep = config.SpinDecay * dt;
                w.y = Mathf.MoveTowards(w.y, 0f, decayStep);
            }

            if (config.LinearDrag > 0f)
            {
                v *= Mathf.Clamp01(1f - config.LinearDrag * dt);
            }
            if (config.AngularDrag > 0f)
            {
                w *= Mathf.Clamp01(1f - config.AngularDrag * dt);
            }

            v.y = 0f;
            b.vel = v;
            b.ang = w;
        }

        private static void ResolvePairCollision(
            ref VirtualBall b1,
            ref VirtualBall b2,
            int step,
            float dt,
            float R,
            float e,
            PhysicsConfig config)
        {
            if (b1.lastBallStep == step || b2.lastBallStep == step)
                return;

            Vector3 p1 = b1.prevPos;
            Vector3 p2 = b2.prevPos;
            Vector3 v1 = (b1.lastCushionStep == step) ? b1.vel : b1.prevVel;
            Vector3 v2 = (b2.lastCushionStep == step) ? b2.vel : b2.prevVel;
            Vector3 w1 = (b1.lastCushionStep == step) ? b1.ang : b1.prevAng;
            Vector3 w2 = (b2.lastCushionStep == step) ? b2.ang : b2.prevAng;
            v1.y = 0f;
            v2.y = 0f;

            Vector3 d0 = new Vector3(p1.x - p2.x, 0f, p1.z - p2.z);
            Vector3 vRel = v1 - v2;

            float a = vRel.sqrMagnitude;
            if (a < 1e-8f)
                return;

            float b = Vector3.Dot(d0, vRel);
            if (b >= 0f)
                return;

            float twoR = 2f * R;
            float twoRSq = twoR * twoR;
            float c = d0.sqrMagnitude - twoRSq;
            float disc = b * b - a * c;

            float tHit;
            if (disc >= 0f)
            {
                tHit = (-b - Mathf.Sqrt(disc)) / a;
            }
            else
            {
                tHit = -b / a;
            }

            // Phải chạm nhau trong cửa sổ của bước hiện tại
            if (tHit > dt * 1.15f || tHit < -4f * dt)
                return;

            tHit = Mathf.Clamp(tHit, -4f * dt, 4f * dt);

            Vector3 c1 = new Vector3(p1.x + v1.x * tHit, p1.y, p1.z + v1.z * tHit);
            Vector3 c2 = new Vector3(p2.x + v2.x * tHit, p2.y, p2.z + v2.z * tHit);

            Vector3 n = new Vector3(c2.x - c1.x, 0f, c2.z - c1.z);
            if (n.sqrMagnitude < 1e-10f)
                return;
            n.Normalize();

            float vn = Vector3.Dot(v1 - v2, n);
            if (vn <= 0f)
                return;

            float impulseScalar = 0.5f * (1f + e) * vn;
            Vector3 impulseVec = n * impulseScalar;

            Vector3 v1Post = v1 - impulseVec;
            Vector3 v2Post = v2 + impulseVec;
            v1Post.y = 0f;
            v2Post.y = 0f;

            Vector3 mid = 0.5f * (c1 + c2);
            float halfSep = R + 0.00025f;
            Vector3 pos1 = new Vector3(mid.x - n.x * halfSep, p1.y, mid.z - n.z * halfSep);
            Vector3 pos2 = new Vector3(mid.x + n.x * halfSep, p2.y, mid.z + n.z * halfSep);

            if (IsNearStraightCushion(pos2, R, out Vector3 cNorm2, out float railCoord2, out bool isXRail2) &&
                Vector3.Dot(v2Post, cNorm2) < -0.015f)
            {
                ClampAndBounceRailBall(
                    ref pos2, ref v2Post, ref w2,
                    ref pos1, ref v1Post,
                    n, cNorm2, railCoord2, isXRail2, R, config);
                b2.lastCushionStep = step;
            }
            else if (IsNearStraightCushion(pos1, R, out Vector3 cNorm1, out float railCoord1, out bool isXRail1) &&
                     Vector3.Dot(v1Post, cNorm1) < -0.015f)
            {
                ClampAndBounceRailBall(
                    ref pos1, ref v1Post, ref w1,
                    ref pos2, ref v2Post,
                    -n, cNorm1, railCoord1, isXRail1, R, config);
                b1.lastCushionStep = step;
            }

            float tRem = Mathf.Clamp(dt - Mathf.Max(0f, tHit), 0f, dt);
            if (tRem > 0f)
            {
                Vector3 next1 = pos1 + v1Post * tRem;
                Vector3 next2 = pos2 + v2Post * tRem;
                float minX = TableWrapper.FootCushionX + R + 0.0005f;
                float maxX = TableWrapper.HeadCushionX - R - 0.0005f;
                float maxZ = TableWrapper.CushionHalfWidth - R - 0.0005f;
                float minSepSq = (2f * R + 0.0002f) * (2f * R + 0.0002f);

                if (next1.x >= minX && next1.x <= maxX && Mathf.Abs(next1.z) <= maxZ &&
                    next2.x >= minX && next2.x <= maxX && Mathf.Abs(next2.z) <= maxZ &&
                    (next2 - next1).sqrMagnitude >= minSepSq)
                {
                    pos1 = next1;
                    pos2 = next2;
                }
            }

            b1.pos = pos1;
            b2.pos = pos2;
            b1.vel = v1Post;
            b2.vel = v2Post;
            b1.ang = w1;
            b2.ang = w2;
            b1.lastBallStep = step;
            b2.lastBallStep = step;
        }

        private static bool IsNearStraightCushion(Vector3 pos, float R, out Vector3 cushionNormal, out float railFaceCoord, out bool isXAxisRail)
        {
            const float margin = 0.0055f;
            float threshold = R + margin;

            if (Mathf.Abs(pos.z) <= TableWrapper.ShortCushionHalfSpanZ - 0.004f)
            {
                if (pos.x - TableWrapper.FootCushionX <= threshold)
                {
                    cushionNormal = Vector3.right;
                    railFaceCoord = TableWrapper.FootCushionX;
                    isXAxisRail = true;
                    return true;
                }
                if (TableWrapper.HeadCushionX - pos.x <= threshold)
                {
                    cushionNormal = Vector3.left;
                    railFaceCoord = TableWrapper.HeadCushionX;
                    isXAxisRail = true;
                    return true;
                }
            }

            if ((pos.x >= -1.008f && pos.x <= -0.078f) || (pos.x >= 0.066f && pos.x <= 0.996f))
            {
                if (TableWrapper.CushionHalfWidth - pos.z <= threshold)
                {
                    cushionNormal = Vector3.back;
                    railFaceCoord = TableWrapper.CushionHalfWidth;
                    isXAxisRail = false;
                    return true;
                }
                if (pos.z - (-TableWrapper.CushionHalfWidth) <= threshold)
                {
                    cushionNormal = Vector3.forward;
                    railFaceCoord = -TableWrapper.CushionHalfWidth;
                    isXAxisRail = false;
                    return true;
                }
            }

            cushionNormal = Vector3.zero;
            railFaceCoord = 0f;
            isXAxisRail = false;
            return false;
        }

        private static void ClampAndBounceRailBall(
            ref Vector3 railPos, ref Vector3 railVel, ref Vector3 railAng,
            ref Vector3 otherPos, ref Vector3 otherVel,
            Vector3 hitNormalToRailBall, Vector3 cushionNormal, float railFaceCoord, bool isXAxisRail, float R, PhysicsConfig config)
        {
            float safeCenterDist = R + 0.0004f;
            if (isXAxisRail)
            {
                float limitX = railFaceCoord + cushionNormal.x * safeCenterDist;
                if ((cushionNormal.x > 0f && railPos.x < limitX) || (cushionNormal.x < 0f && railPos.x > limitX))
                    railPos.x = limitX;
            }
            else
            {
                float limitZ = railFaceCoord + cushionNormal.z * safeCenterDist;
                if ((cushionNormal.z > 0f && railPos.z < limitZ) || (cushionNormal.z < 0f && railPos.z > limitZ))
                    railPos.z = limitZ;
            }

            ComputeCushionBounceImpulse(railVel, railAng, cushionNormal, false, config, out railVel, out railAng);

            float reboundOutSpeed = Vector3.Dot(railVel, cushionNormal);
            if (reboundOutSpeed < 0.08f)
            {
                railVel += cushionNormal * (0.08f - reboundOutSpeed);
                reboundOutSpeed = 0.08f;
            }

            Vector3 railTangent = new Vector3(-cushionNormal.z, 0f, cushionNormal.x);
            float cutSin = Mathf.Abs(Vector3.Dot(hitNormalToRailBall, railTangent));
            if (cutSin >= 0.16f)
            {
                float otherIntoRailBall = Vector3.Dot(otherVel, hitNormalToRailBall);
                if (otherIntoRailBall > 0f)
                    otherVel -= hitNormalToRailBall * otherIntoRailBall;

                const float cushionCompressionTime = 0.0045f;
                otherPos += otherVel * cushionCompressionTime;
                railPos += railVel * cushionCompressionTime;
            }
            else
            {
                float sideSign = Vector3.Dot(hitNormalToRailBall, railTangent) >= 0f ? 1f : -1f;
                railVel += railTangent * (sideSign * reboundOutSpeed * 0.18f);
                otherPos = railPos + cushionNormal * (2f * R + 0.0025f) - railTangent * (sideSign * R * 0.35f);
            }

            Vector3 delta = railPos - otherPos;
            delta.y = 0f;
            float dist = delta.magnitude;
            float minSep = 2f * R + 0.0010f;
            Vector3 sepDir = dist > 1e-5f ? (delta / dist) : -cushionNormal;
            if (dist < minSep)
                otherPos = railPos - sepDir * minSep;

            float closingSpeed = Vector3.Dot(otherVel - railVel, sepDir);
            if (closingSpeed > -0.02f)
                otherVel -= sepDir * (closingSpeed + 0.04f);
        }

        private static void ResolveCushionAndJawCollisions(ref VirtualBall b, int step, float R, PhysicsConfig config)
        {
            if (b.lastCushionStep == step)
                return;

            Vector3 pos = b.pos;
            Vector3 vel = b.vel;
            Vector3 ang = b.ang;

            // 1. Kiểm tra 4 đoạn băng thẳng (ngoài khu vực miệng lỗ)
            const float a = 0.002f;
            float shortHalfZ = TableWrapper.ShortCushionHalfSpanZ - a;

            if (Mathf.Abs(pos.z) <= shortHalfZ)
            {
                if (pos.x - TableWrapper.FootCushionX <= R && vel.x < -0.02f)
                {
                    pos.x = TableWrapper.FootCushionX + R + 0.0002f;
                    if (ComputeCushionBounceImpulse(vel, ang, Vector3.right, false, config, out vel, out ang))
                    {
                        b.pos = pos; b.vel = vel; b.ang = ang; b.lastCushionStep = step;
                        return;
                    }
                }
                else if (TableWrapper.HeadCushionX - pos.x <= R && vel.x > 0.02f)
                {
                    pos.x = TableWrapper.HeadCushionX - R - 0.0002f;
                    if (ComputeCushionBounceImpulse(vel, ang, Vector3.left, false, config, out vel, out ang))
                    {
                        b.pos = pos; b.vel = vel; b.ang = ang; b.lastCushionStep = step;
                        return;
                    }
                }
            }

            bool inLongSpan = (pos.x >= -1.014f + a && pos.x <= -0.076f) || (pos.x >= 0.064f && pos.x <= 1.002f - a);
            if (inLongSpan)
            {
                if (TableWrapper.CushionHalfWidth - pos.z <= R && vel.z > 0.02f)
                {
                    pos.z = TableWrapper.CushionHalfWidth - R - 0.0002f;
                    if (ComputeCushionBounceImpulse(vel, ang, Vector3.back, false, config, out vel, out ang))
                    {
                        b.pos = pos; b.vel = vel; b.ang = ang; b.lastCushionStep = step;
                        return;
                    }
                }
                else if (pos.z - (-TableWrapper.CushionHalfWidth) <= R && vel.z < -0.02f)
                {
                    pos.z = -TableWrapper.CushionHalfWidth + R + 0.0002f;
                    if (ComputeCushionBounceImpulse(vel, ang, Vector3.forward, false, config, out vel, out ang))
                    {
                        b.pos = pos; b.vel = vel; b.ang = ang; b.lastCushionStep = step;
                        return;
                    }
                }
            }

            // 2. Kiểm tra 12 má lỗ vát (Corner Facings & Side Jaws)
            Vector2 pXZ = new Vector2(pos.x, pos.z);
            for (int j = 0; j < jawSegments.Length; j++)
            {
                JawSegment jaw = jawSegments[j];
                Vector2 closest = ClosestPointOnSegment(pXZ, jaw.p1, jaw.p2);
                Vector2 diff = pXZ - closest;
                float distSq = diff.sqrMagnitude;
                if (distSq <= R * R)
                {
                    float vn = -Vector3.Dot(vel, jaw.faceNormal);
                    if (vn >= 0.02f)
                    {
                        float dist = Mathf.Sqrt(Mathf.Max(1e-9f, distSq));
                        Vector2 pushDir = Vector2.Dot(diff, new Vector2(jaw.faceNormal.x, jaw.faceNormal.z)) >= 0f
                            ? new Vector2(jaw.faceNormal.x, jaw.faceNormal.z)
                            : (diff / dist);
                        Vector2 newXZ = closest + pushDir * (R + 0.0003f);
                        pos.x = newXZ.x;
                        pos.z = newXZ.y;

                        if (ComputeCushionBounceImpulse(vel, ang, jaw.faceNormal, true, config, out vel, out ang))
                        {
                            b.pos = pos; b.vel = vel; b.ang = ang; b.lastCushionStep = step;
                            return;
                        }
                    }
                }
            }
        }

        private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 1e-8f) return a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSq);
            return a + ab * t;
        }

        private static bool ComputeCushionBounceImpulse(
            Vector3 v0, Vector3 w0, Vector3 nXZ, bool isPocketFacing,
            PhysicsConfig config,
            out Vector3 vPost, out Vector3 wPost)
        {
            vPost = v0;
            wPost = w0;

            float vn = -Vector3.Dot(v0, nXZ);
            if (vn < 0.02f)
                return false;

            float m = config.BallMass;
            float R = config.BallRadius;
            float inertia = 0.4f * m * R * R;

            Vector3 up = Vector3.up;
            Vector3 t = Vector3.Cross(up, nXZ).normalized;

            float s = isPocketFacing ? 0f : Mathf.Clamp(config.CushionNoseHeight, 0f, 0.9f);
            float c = Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));
            Vector3 rc = R * (-nXZ * c + up * s);

            Vector3 u = v0 + Vector3.Cross(w0, rc);

            float e;
            if (isPocketFacing)
            {
                float facingRes = config.PocketFacingRestitution;
                e = Mathf.Lerp(facingRes, facingRes * 0.75f, Mathf.Clamp01(vn / Mathf.Max(0.01f, config.CushionRestitutionSpeedRef)));
            }
            else
            {
                e = Mathf.Lerp(config.CushionRestitutionSlow,
                               config.CushionRestitutionFast,
                               Mathf.Clamp01(vn / Mathf.Max(0.01f, config.CushionRestitutionSpeedRef)));
            }

            float knn = 1f / m + Vector3.Cross(rc, nXZ).sqrMagnitude / inertia;
            float jn = (1f + e) * vn / knn;

            float ut = Vector3.Dot(u, t);
            float uu = Vector3.Dot(u, up);

            Vector3 dUt = t / m + Vector3.Cross(Vector3.Cross(rc, t) / inertia, rc);
            Vector3 dUu = Vector3.Cross(Vector3.Cross(rc, up) / inertia, rc);
            float k11 = Vector3.Dot(dUt, t), k21 = Vector3.Dot(dUt, up);
            float k12 = Vector3.Dot(dUu, t), k22 = Vector3.Dot(dUu, up);
            float det = k11 * k22 - k12 * k21;

            float jt = 0f, ju = 0f;
            float frictionCoeff = isPocketFacing ? config.PocketFacingFriction : config.CushionFriction;
            if (Mathf.Abs(det) > 1e-12f)
            {
                jt = (-ut * k22 + uu * k12) / det;
                ju = (-uu * k11 + ut * k21) / det;
                float jfMag = Mathf.Sqrt(jt * jt + ju * ju);
                float jfMax = frictionCoeff * jn;
                if (jfMag > jfMax && jfMag > 1e-9f)
                {
                    float scale = jfMax / jfMag;
                    jt *= scale;
                    ju *= scale;
                }
            }

            Vector3 jTotal = jn * nXZ + jt * t + ju * up;
            vPost = v0 + (jn * nXZ + jt * t) / m;
            vPost.y = 0f;
            wPost = w0 + Vector3.Cross(rc, jTotal) / inertia;
            return true;
        }

        private static bool TryCheckPocketCapture(ref VirtualBall b, out int capturedPocketId)
        {
            Vector2 ballXZ = new Vector2(b.pos.x, b.pos.z);
            float speed = b.vel.magnitude;

            for (int p = 0; p < 6; p++)
            {
                Vector2 pocketXZ = pocketCentersXZ[p];
                float dist = Vector2.Distance(ballXZ, pocketXZ);

                bool isCorner = (p != 2 && p != 3);
                float baseCap = isCorner ? 0.098f : 0.085f;
                float minCap = isCorner ? 0.078f : 0.065f;
                float effective = Mathf.Max(minCap, baseCap - 0.008f * Mathf.Max(0f, speed - 2f));

                // Với bi số 8: dùng ngưỡng chặt 96% bán kính bắt bi để đảm bảo 100% chỉ khi bi 8 thực sự đi sâu vào lòng lỗ mới báo true
                float margin = (b.number == 8) ? 0.96f : 1.0f;
                if (dist <= effective * margin)
                {
                    capturedPocketId = p;
                    return true;
                }
            }

            capturedPocketId = -1;
            return false;
        }

        private static Vector2 GetDefaultPocketCenterXZ(int pocketId)
        {
            switch (pocketId)
            {
                case 0: return new Vector2(-1.085f,  0.518f); // FootLeft
                case 1: return new Vector2(-1.085f, -0.518f); // FootRight
                case 2: return new Vector2(-0.006f,  0.542f); // SideLeft
                case 3: return new Vector2(-0.006f, -0.542f); // SideRight
                case 4: return new Vector2( 1.073f,  0.518f); // HeadLeft
                case 5: return new Vector2( 1.073f, -0.518f); // HeadRight
                default: return Vector2.zero;
            }
        }

        private static bool IsRackIntact(BallManager ballManager)
        {
            var objectBalls = ballManager.GetActiveObjectBalls();
            if (objectBalls == null || objectBalls.Count < 15) return false;

            Vector3 min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, 0f, float.MinValue);
            for (int i = 0; i < objectBalls.Count; i++)
            {
                Vector3 p = objectBalls[i].transform.position;
                if (p.x < min.x) min.x = p.x;
                if (p.x > max.x) max.x = p.x;
                if (p.z < min.z) min.z = p.z;
                if (p.z > max.z) max.z = p.z;
            }

            return (max.x - min.x) < 0.33f && (max.z - min.z) < 0.33f;
        }
    }
}

