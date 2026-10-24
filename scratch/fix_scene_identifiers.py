import re

def fix_scene(file_path):
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()

    # 1. BallsRoot GameObject header
    content = content.replace(
        "--- !u!1 &700000020\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: 700000021}\n  m_Layer: 0\n  m_Name: BallsRoot",
        "--- !u!1 &700000050\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: 700000051}\n  m_Layer: 0\n  m_Name: BallsRoot"
    )

    # 2. BallsRoot Transform header and GameObject ref
    content = content.replace(
        "--- !u!4 &700000021\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 700000020}",
        "--- !u!4 &700000051\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 700000050}"
    )

    # 3. Balls parent references in Ball Transform 0..15
    content = content.replace("    m_TransformParent: {fileID: 700000021}\n", "    m_TransformParent: {fileID: 700000051}\n")

    # 4. Scene roots order containing BallsRoot Transform
    # In SceneRoots / m_Roots list:
    content = re.sub(r'(m_Roots:\n(?:  - \{fileID: \d+\}\n)*)  - \{fileID: 700000021\}', r'\1  - {fileID: 700000051}', content)

    with open(file_path, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"Fixed scene: {file_path}")

fix_scene("Assets/Game/Scenes/Gameplay.unity")
fix_scene("Assets/Game/Scenes/Lobby.unity")
