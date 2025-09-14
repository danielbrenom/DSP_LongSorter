# LongSorter 

Mod for Dyson Sphere Program. Needs BepInEx. Forked from hetima/DSP_LongSorter.

Hold down `Ctrl` key when building the sorter to loosen the length and angle restriction (about up to 5x). The sorters created will continue to work even if this mod is disabled.  
If the building candidate is too skewed, you can press the `Shift` key at the same time to increase restrictions of the angle and set it straight.

## Known Issue
If a long sorter is included when copying an object, the length will be reset at the destination, which may result in an unexpected connection state.

It does not support blueprint.

---

## 説明

ソーターを設置するときに `Ctrl` キーを押していると、長さや角度の制限を緩めます(約5倍)。作成したソーターはこの mod を無効にしても作動し続けます。  
建築候補が斜めになって望み通りにできないときは、`Shift` キーも同時に押すと角度の制限を強めて真っ直ぐに設置することができます。

## 既知の問題
オブジェクトのコピー時に長いソーターが含まれていると、コピー先では長さがリセットされ予期しない接続状態になることがあります。

ブループリントには対応していません。

---

## Release Notes

### v1.3.2
- Update for 0.10.32.25779 game version
- Removed embedded game file dependency
- Changed build opcode usage to prevent breaking

### v1.2.2
- Update for 0.9.27.15466 game version

### v1.2.1

- Update for 0.9.24.11209 game version

### v1.2.0

- It is now possible to connect vertically, ignoring the collision detection with the belt / ベルトとの衝突判定を無視して垂直に接続できるようになりました
- Pressing the `Shift` key at the same time to increase restrictions of the angle and set it straight. / `Shift` キーも同時に押すと角度の制限を強めて真っ直ぐに設置するようになりました

### v1.1.0

- From now on, it only works when `Ctrl` key is pressed / Ctrl キーを押しているときだけ作用するようにまりました
- Ease restrictions of Angle too / 角度制限も緩和されるようにまりました

### v1.0.0

- Initial Release

