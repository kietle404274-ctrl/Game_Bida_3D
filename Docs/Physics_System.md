# Tài liệu hệ thống vật lý – Game Bida 3D (WPA 8-Ball)

> Phiên bản: sau đợt nâng cấp "vật lý chân thực". Các số liệu là giá trị khởi điểm, chỉnh được trong `PhysicsConfig` / `Assets/Game/Configs/DefaultPhysicsConfig.asset`.

## 1. Tổng quan kiến trúc

Game dùng **PhysX của Unity** làm lõi (va chạm bi–bi, chặn bi bằng băng/bàn) và **bổ sung lớp vật lý tự viết** cho những thứ PhysX không mô phỏng tốt: xoáy (spin), ma sát trượt/lăn với nỉ, và phản hồi băng.

| Thành phần | File | Vai trò |
|---|---|---|
| Cấu hình | `Physics/PhysicsConfig.cs` | Toàn bộ hằng số vật lý (ScriptableObject) |
| Cú đánh | `Physics/ShotExecutor.cs` | Đổi lực đánh + điểm chạm cơ thành vận tốc và xoáy ban đầu |
| Bi | `Physics/BallController.cs` | Ma sát nỉ (trượt/lăn), phản hồi băng, lưu trạng thái trước bước PhysX |
| Bàn | `Physics/TableWrapper.cs` | Tạo collider nỉ, băng, má lỗ và vật liệu vật lý |
| Lỗ | `Physics/PocketDetector.cs` | Xác định bi rơi lỗ (có xét tốc độ) |
| Khởi tạo | `Core/GameplayInitializer.cs` | Thiết lập `Physics.*` toàn cục, `fixedDeltaTime` |

Thứ tự mỗi bước vật lý (`Time.fixedDeltaTime = 0.01 s`):

1. `BallController.FixedUpdate` → `PhysicsTick()` (ma sát nỉ, suy giảm xoáy đứng).
2. Lưu `prevVelocity`, `prevAngular` (trạng thái **ngay trước** bước PhysX).
3. PhysX giải va chạm. Nếu bi chạm băng → `OnCollisionEnter` → `HandleCushionCollisionResponse` áp xung lực hiệu chỉnh.

### Hằng số cơ bản

| Ký hiệu | Giá trị | Ý nghĩa |
|---|---|---|
| m | 0,17 kg | Khối lượng bi |
| R | 0,02855 m | Bán kính bi (Ø 57,15 mm) |
| I | 0,4·m·R² | Mô-men quán tính bi đặc |
| g | 9,81 m/s² | Gia tốc trọng trường |
| μ_nỉ | 0,20 | Ma sát trượt bi–nỉ (`clothFriction`) |
| μ_lăn | 0,015 | Hệ số cản lăn (`rollingResistance`) |
| Δt | 0,01 s | Bước vật lý cố định |

Hệ trục: bàn dài theo **X**, ngang theo **Z**, **Y** hướng lên. Bi bị khóa vị trí Y (chỉ chuyển động trên mặt phẳng XZ).

---

## 2. Cú đánh (`ShotExecutor`)

### 2.1 Vận tốc ban đầu

Xung lực đánh theo hướng `d` (đơn vị, trên XZ):

```
J = p · J_max · d          (p ∈ [0,1] là lực; cú phá bi nhân thêm 1,6)
v₀ = J / m
```

### 2.2 Điểm chạm cơ và xoáy

Điểm chạm trên mặt bi (UI chọn tâm bi) `c ∈ đĩa đơn vị`. Độ lệch thực:

```
b = c · 0,6        (đơn vị bán kính R; mép UI tương ứng 0,6R)
```

Bi bị đánh lệch tâm: một phần xung lực biến thành xoáy nên bi cái chậm hơn:

```
v = v₀ / (1 + k_loss · |b|²)        (k_loss = OffsetSpeedLoss = 1)
```

Xoáy sinh ra do đánh ở độ cao `b_y·R` so với tâm (xung ngang J tạo mô-men J·b·R, I = 0,4mR²):

```
ω = 2,5 · v · b / R        (nhân với SpinStrength = 1)
```

- **Cu-lê / trô** (b_y): quay quanh trục ngang `a = up × d`, độ lớn `ω_top = 2,5·v·b_y/R`.
  `b_y > 0` = cu-lê (follow), `b_y < 0` = trô (draw/backspin).
- **Ép-phê** (b_x): quay quanh trục Y, `ω_side = −2,5·v·b_x/R` (ép phải → ω_y < 0).
- **Không mô phỏng squirt** (bi cái không lệch hướng khi ép-phê) – theo yêu cầu thiết kế.

Ví dụ: v = 3 m/s, b_y = 0,6 → ω ≈ 2,5·3·0,6/0,02855 ≈ 158 rad/s (nên `maxAngularVelocity` nâng lên 1000).

---

## 3. Ma sát nỉ (`BallController.ApplyClothSpinInteraction`)

Điểm chạm bi–nỉ có vị trí `r_c = (0, −R, 0)`. Vận tốc trượt tại điểm đó:

```
v_slip = v + ω × r_c = ( v.x + ω.z·R , 0 , v.z − ω.x·R )
```

### 3.1 Pha trượt (|v_slip| > 0,015 m/s)

Ma sát Coulomb chống lại trượt:

```
F = −μ_nỉ · m · g · v̂_slip
```

Chống rung quanh 0 (ma sát không được vượt mức đủ để triệt trượt trong đúng 1 bước):

```
F_max = |v_slip| · m / (Δt · 3,5)
|F| = min(μ_nỉ·m·g, F_max)
```

Hệ số 3,5 = 1 + m·R²/I = 1 + 2,5 (cả tịnh tiến lẫn quay cùng giảm trượt).

Mô-men: `τ = r_c × F = (−R·F_z, 0, R·F_x)`.

Kết quả đúng thực tế: bi trô sẽ lùi, bi cu-lê tăng tốc, sau thời gian trượt thì chuyển sang lăn thuần.

### 3.2 Pha lăn thuần (v_slip ≈ 0)

Khóa quan hệ lăn không trượt và áp lực cản lăn:

```
ω = ( v.z/R , ω_y , −v.x/R )
a_cản = μ_lăn · g          (giảm đều, bi dừng tự nhiên)
```

Với μ_lăn = 0,015: quãng dừng `s = v²/(2·μ_lăn·g) = v²/0,294`. Ví dụ v = 2 m/s → khoảng 13,6 m (bi sẽ dội băng nhiều lần, thực tế bàn mất nhiều năng lượng ở băng).

### 3.3 Suy giảm xoáy đứng (ép-phê)

```
ω_y ← MoveTowards(ω_y, 0, SpinDecay · Δt)       (SpinDecay = 25 rad/s²)
```

---

## 4. Va chạm bi–bi (`HandleBallCollisionResponse`)

- **Va chạm đơn / cắt góc (Isolated 2-Ball Collision):** Giải chính xác thời điểm tiếp xúc liên tục (Continuous Time-of-Impact – TOI) trong bước vật lý từ `prevPosition`, `prevVelocity`:
  $$\| (\vec{p}_1 + \vec{v}_1 t^*) - (\vec{p}_2 + \vec{v}_2 t^*) \| = 2R$$
  Tại đúng vị trí Ghost Ball $\vec{p}_1(t^*), \vec{p}_2(t^*)$, pháp tuyến đơn vị $\vec{n} = (\vec{c}_2 - \vec{c}_1) / (2R)$ trùng khít 100% với tia chỉ hướng của thước ngắm (`AimGuide`). Xung lực đàn hồi dọc theo $\vec{n}$ với hệ số nảy $e = 0,95$ (`BallRestitution`), ma sát mặt bi = 0 để không sinh *collision throw* làm lệch hướng bi mục tiêu.
- **Va chạm cụm nhiều bi / Cú phá bi (Multi-ball Cluster / Rack Break):** Khi phát hiện có bi thứ 3 nằm sát trong bán kính $2,15R$, hệ thống tự động nhường cho bộ giải đa vật thể PGS 12-iteration của PhysX để dàn bi 15 quả nổ tự nhiên.
- **Bảo toàn xoáy qua va chạm:** Vận tốc góc `prevAngular` được giữ nguyên qua thời điểm chạm nên cú bắn thẳng không xoáy làm bi cái dừng tại chỗ (stop shot), có xoáy trô sẽ giật lùi và cu-lê sẽ đuổi theo trên mặt nỉ như thực tế.

---

## 5. Va chạm băng (`HandleCushionCollisionResponse`)

PhysX chỉ **chặn** bi (băng có `bounciness = 0`, `friction = 0`). Phần phản hồi thật được tính bằng mô hình xung lực vật rắn:

### 5.1 Hình học

- `n`: pháp tuyến băng, hướng từ băng ra phía bi (nằm ngang).
- `t = up × n`: tiếp tuyến dọc băng.
- Mũi băng cao hơn tâm bi `s·R` (`CushionNoseHeight s = 0,27`). Điểm chạm trên bi:

```
c = √(1 − s²)
r_c = R · ( −c·n + s·up )
```

- Dùng `v₀ = prevVelocity`, `ω₀ = prevAngular` (trước khi PhysX triệt thành phần pháp tuyến).

### 5.2 Vận tốc điểm chạm

```
u = v₀ + ω₀ × r_c
u_n = u · n          (< 0 khi đang lao vào băng; bỏ qua nếu v_n < 0,02 m/s)
```

### 5.3 Xung pháp tuyến

Hệ số nảy phụ thuộc tốc độ (băng cứng hơn khi chậm, hao nhiều khi nhanh):

```
e = Lerp(e_slow = 0,60 , e_fast = 0,44 , clamp01(v_n / 3,5))
```

Độ cứng pháp tuyến hiệu dụng và xung:

```
K_nn = 1/m + |r_c × n|² / I
J_n  = −(1 + e) · u_n / K_nn
```

### 5.4 Xung ma sát (Coulomb)

Ma sát nằm trong mặt phẳng (t, up). Thành phần tuyến tính theo Y bị loại (bi bị mặt nỉ giữ), nên xây ma trận độ cứng 2×2:

```
Δu(t)  = t/m + ((r_c × t)/I) × r_c
Δu(up) = ((r_c × up)/I) × r_c
K_ff = [ Δu(t)·t   Δu(up)·t  ]
       [ Δu(t)·up  Δu(up)·up ]
```

Giải xung bám hoàn toàn rồi kẹp theo nón ma sát:

```
J_f = −K_ff⁻¹ · (u·t , u·up)
nếu |J_f| > μ_băng · J_n  →  J_f ← J_f · (μ_băng·J_n / |J_f|)      (μ_băng = 0,16)
```

### 5.5 Áp xung

```
J = J_n·n + J_t·t + J_u·up
v' = v₀ + (J_n·n + J_t·t)/m            (Y = 0)
ω' = ω₀ + (r_c × J) / I
rb.velocity = v'
rb.angularVelocity = ω'
```

> **Lưu ý chống kích tốc độ PhysX:** Gán trực tiếp `rb.velocity = v'` thay vì cộng dồn hiệu số với PhysX, triệt tiêu hoàn toàn lực đẩy chống xuyên thấu (depenetration impulse) do PhysX sinh ra tại các góc cạnh nhọn của collider.

### 5.6 Phân biệt Băng chính (Cushion) và Má lỗ (Pocket Facing / Jaw)

Ngoài đời thực, má lỗ được dán miếng đệm da/cao su giảm chấn để triệt tiêu lực nảy và giúp bi lọt vào túi:

| Thuộc tính | Băng cao su chính (`Cushion_*`) | Má lỗ / Mép túi (`Facing_*`, `Jaw_*`) |
|---|---|---|
| Độ cao mũi `s` | `cushionNoseHeight = 0,27R` | `0` (mặt phẳng đứng, không sinh mô-men đẩy thêm) |
| Hệ số nảy `e` | Chuẩn WPA: `0,60 → 0,44` (giảm theo tốc độ) | Thấp: `pocketFacingRestitution = 0,42 → 0,30` |
| Ma sát `μ` | Chuẩn WPA: `cushionFriction = 0,16` | Cao: `pocketFacingFriction = 0,25` (hãm trượt bi vào lỗ) |

### 5.7 Hiệu ứng thu được

| Hiện tượng | Nguồn gốc trong mô hình |
|---|---|
| Bi cu-lê/trô đổi xoáy lăn sau khi chạm băng | `r_c` có thành phần `s·up` → mô-men quanh trục `t` |
| Ép-phê mở/thu góc bật | `J_t` từ ma sát băng khi ω_y ≠ 0 |
| Bật băng nhanh mất nhiều năng lượng hơn | `e` giảm theo v_n |
| Góc bật ≈ góc tới khi không xoáy | `e` gần 1, μ_băng nhỏ |
| Liếm mép lỗ bi giảm tốc êm ái, rơi tọt xuống túi | Má lỗ có `e = 0,42`, `s = 0`, `μ = 0,25` triệt tiêu > 55% lực |

---

## 6. Rơi lỗ (`PocketDetector`)

Bi rơi khi khoảng cách tâm bi–tâm lỗ trên XZ ≤ bán kính hiệu dụng; bi nhanh phải vào sâu hơn:

```
r_eff = max( r_min = 0,065 , r_0 − 0,008 · max(0, |v| − 2) )     (r_0 = 0,085 m)
```

---

## 7. Cài đặt PhysX toàn cục (`ApplyGlobalPhysicsSettings`)

| Thiết lập | Giá trị | Lý do |
|---|---|---|
| `Time.fixedDeltaTime` | 0,01 s | Chính xác hơn khi bi nhanh/xoáy lớn |
| `defaultSolverIterations` | 12 | Ổn định va chạm bi–bi |
| `defaultSolverVelocityIterations` | 4 | Giải vận tốc chính xác |
| `bounceThreshold` | 0,05 | Cho phép nảy ở tốc độ thấp |
| `defaultMaxAngularSpeed`, `maxAngularVelocity` | 1000 rad/s | Xoáy vật lý có thể vượt 100 rad/s |

---

## 8. Bảng tham số điều chỉnh

| Tham số | Mặc định | Tăng thì… |
|---|---|---|
| `rollingResistance` | 0,015 | Bi dừng sớm hơn |
| `linearDrag` | 0 | Bi dừng sớm (nên để 0 nếu muốn chân thực) |
| `clothFriction` | 0,20 | Xoáy nỉ tác động mạnh/nhanh hơn |
| `spinStrength` | 1,0 | Xoáy mạnh hơn thực tế |
| `maxContactOffset` | 0,6 | Cho đánh lệch tâm xa hơn |
| `offsetSpeedLoss` | 1,0 | Đánh lệch tâm hao tốc độ nhiều hơn |
| `spinDecay` | 25 | Ép-phê tắt nhanh hơn |
| `cushionNoseHeight` | 0,27 | Đổi xoáy lăn khi chạm băng mạnh hơn |
| `cushionFriction` | 0,12 | Ép-phê đổi góc bật nhiều hơn |
| `cushionRestitutionSlow/Fast/SpeedRef` | 0,85/0,60/6 | Băng nảy mạnh hơn |

## 8b. Cú nhảy bi (jump shot)

Người chơi nâng cơ một góc θ (phím K, 0–60°). `ShotExecutor` tách vận tốc:

```
v_ngang = v · cos θ
v_đứng  = min( v · sin θ · JumpGain , MaxJumpSpeed )        (chỉ khi θ ≥ MinJumpAngle = 8°, không áp dụng cú phá bi)
```

`JumpGain = 0,6` thay cho việc mô phỏng phản lực của mặt đá; `MaxJumpSpeed = 2 m/s` (cao tối đa ≈ 0,2 m).
Độ cao đỉnh `h = v_đứng² / 2g`, thời gian bay `t = 2·v_đứng / g`, tầm bay `R = v_ngang · t` (dùng cho thước ngắm).

`BallController.LaunchJump` mở khóa trục Y; trong lúc bay không có ma sát nỉ / phản hồi băng. Khi bi chạm lại nỉ
(`y ≤ y_nỉ + 1,5 mm` và `|v_y| < 0,15 m/s`) bi bị khóa Y trở lại. Nỉ có độ nảy `ClothBounce = 0,25` nên bi nảy 1–2 lần.
Bi bay quá cao có thể văng khỏi bàn và bị phạt theo luật "bi văng khỏi bàn". `PocketDetector` không bắt bi khi bi đang cao hơn mặt nỉ 2 cm.
AI luôn có `cueElevationDeg = 0` nên không bao giờ nhảy bi.

## 9. Hạn chế đã biết

- Không mô phỏng squirt, không có masse; bi chỉ rời mặt nỉ khi đánh nhảy (mô hình nhảy đơn giản hóa bằng `JumpGain`).
- Va chạm băng giả định pháp tuyến nằm ngang (không xét độ nghiêng của mặt mũi băng).
- Bi–bi dựa vào PhysX (không có mô hình throw riêng).
- Các giá trị mặc định chưa được hiệu chuẩn với số liệu đo thực tế; cần playtest để tinh chỉnh.

## 10. Tài liệu tham khảo

- Dr. Dave Alciatore, *Illustrated Principles of Pool and Billiards* và các tài liệu kỹ thuật tại billiards.colostate.edu (stun/follow/draw, cushion, throw, cloth friction).
- Mathavan et al., "A theoretical analysis of billiard ball dynamics under cushion impacts" (2010).
- Quy chuẩn kích thước bi/bàn WPA.
