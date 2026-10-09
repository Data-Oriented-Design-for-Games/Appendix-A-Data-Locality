# Appendix A — Data Locality

Sample project for **Appendix A** of [*High Performance Unity Game Development (Using data-oriented design)*](https://www.manning.com/books/high-performance-unity-game-development) by Nitzan Wilnai (Manning).

This is a small benchmark, not a game. It moves a large pool of enemies with one line of math, `position += direction * velocity`, and times it. The only thing that changes between cases is how the enemy data is laid out in memory. The question it answers: how much does the layout of your data change the speed of the code that reads it?

## What it shows

- The same update run over four layouts of the same enemy data.
- `EnemyOOP`: a class where the three fields the update uses are spread apart, with unused data between them.
- `EnemyOOP_DataLocality`: the same class with the three used fields moved next to each other.
- `EnemyMove`: a class that holds only the three used fields.
- `EnemyData`: no enemy objects, just one array per field.
- A second test that compares an array of structs with one array per field, for a small struct and for a large one where the loop uses only a few of the fields.

## How the test works

**`Assets/Scripts/Main.cs`** is the test the **RUN** button starts.

- `Padding` is a struct made of eight `Vector4` fields. It stands in for the data an enemy carries that the move code never reads.
- `Awake()` builds a pool of `ArraySize` enemies for each of the four layouts.
- `RunTest()` does `NumIterations` passes. In each pass it moves every enemy in each layout, and adds up the time per layout with `Time.realtimeSinceStartupAsDouble`.

The first two layouts differ only in the order of their fields:

```csharp
public class EnemyOOP
{
    public Vector3 m_position;
    public Padding padding1;
    public Vector3 m_direction;
    public Padding padding2;
    public float m_velocity;
    public Padding padding3;
}
```

In `EnemyOOP_DataLocality`, `m_position`, `m_direction` and `m_velocity` come first and the three paddings come after them. (The constructor and `Move()` are left out here.)

**`Assets/Scripts/SubsetAccessTest.cs`** is a second, stricter test.

- `EnemyNarrow` is a 28-byte struct with only position, direction and velocity. `EnemyWide` is a 128-byte struct with the same three fields plus health, state, timers and other fields.
- Each case runs once over an array of structs and once over separate arrays:
  - **A**: move the narrow enemies. The loop uses every byte.
  - **B**: sum the positions of the wide enemies. The loop reads 12 of 128 bytes.
  - **B'**: move the wide enemies. The loop touches 28 of 128 bytes.
- `Bench()` does one warm-up pass. Then it times `NumIterations` passes with a `Stopwatch`, does that `Repeats` times, and keeps the best time.
- The garbage collector is turned off during the timed part. The last output line, `GC delta`, tells you if anything was allocated anyway.

## Running it

1. Open the project in Unity **6000.3.22f1** (Unity 6.3 LTS) or newer.
2. Open `Assets/Scenes/MainScene.unity` and press **Play**.
3. Click **RUN**. The app freezes while the test runs. The results then appear on screen and in the Console.

The **RUN** button calls `Main.RunTest`. Nothing in the scene calls `SubsetAccessTest.RunTest`. To run the second test, select `Button` in the Hierarchy. Under **On Click ()** in the Inspector, pick the `SubsetAccessTest` object and the `SubsetAccessTest.RunTest` function. Then press **Play** and click **RUN**.

You can change `NumIterations` and `ArraySize` on the `Main` object, and `ArraySize`, `NumIterations` and `Repeats` on the `SubsetAccessTest` object. `Main` builds its data in `Awake`. `SubsetAccessTest` builds its data the first time its `RunTest` runs, so its memory is only used if you run that test. The tooltip on `ArraySize` in `SubsetAccessTest.cs` says 2,000,000 enemies cost about 620 MB. Lower it if your device does not have the room.

## Reading the results

`Main` prints one line per layout:

```
OOP                 <seconds>
OOP Data Locality   <seconds>   <N>x
Optimized OOP       <seconds>   <N>x
Arrays              <seconds>   <N>x
```

- `<seconds>` is the total time for all passes. `<N>x` is the `OOP` time divided by that row's time, so `2x` means the row took half as long as `OOP`.
- `Optimized OOP` is `EnemyMove`. `Arrays` is `EnemyData`.

`SubsetAccessTest` prints two lines for each of A, B and B': `AoS struct[]` and `SoA arrays`. Each line has the time in seconds and the time per enemy in nanoseconds. The `x` value on the `SoA arrays` line is the array-of-structs time divided by the separate-arrays time.

The numbers depend on your device, mostly on the size of its CPU caches, and on whether you run in the Editor or in a build. The comments in `SubsetAccessTest.cs` explain the sizes it uses. They were picked with an Apple M3 in mind.

## More samples

All sample projects for the book: https://github.com/Data-Oriented-Design-for-Games
