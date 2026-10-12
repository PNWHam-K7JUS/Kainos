# Amplifier Band Switching (Xiegu GPA100)

Kainos can tell a **Xiegu GPA100** which band you're on, over Bluetooth, so the amp switches its filters by itself. Other amplifiers that follow an Icom radio over CI-V can work the same way.

Over Bluetooth the GPA100 only follows the **band**. Keep its **ACC1** cable for PTT and ALC.

## Setting it up

1. **On the amp:** hold **AUTO**, choose **BLUETOOTH**, press **OK**, then **EXIT**. Short-press **AUTO** until the display shows **[A]** (automatic band). The amp remembers both.
2. **On the PC:** in Kainos, open **Setup > Serial / Network / Midi CAT > Amplifier (CI-V)** and click **Open Windows Bluetooth settings**. Choose **Add device**, **Bluetooth**, pick **XG_GPA100**, and enter the PIN **0000**.
3. Back in Kainos, click **Find GPA100**. Windows makes two COM ports for the amp; Kainos picks the right one (the outgoing port).
4. Tick **Tell the amplifier the band** and press **OK** or **Apply**.

The status line says **Connected: the amp is following** once the amp starts asking. Change band in Kainos and the amp's display follows within a second or so.

## How it works

The GPA100 is the one asking: about ten times a second it asks "the radio" (it expects an Icom IC-705, CI-V address **A4**) for its frequency and whether it's transmitting. Kainos answers with:

- the **transmit** frequency: VFO B when you're in split, and with a transverter the HL2's own frequency (the amp is on the HL2's output);
- whether Kainos is transmitting (truthfully, but don't rely on it for T/R switching: that's what the ACC1 PTT line is for).

If the amp is switched off or goes out of range, Kainos keeps trying and picks it up again by itself.

## If it doesn't work

- **Find GPA100 says it isn't paired:** pair it in Windows first (step 2).
- **Status stays "Waiting for the amp":** check the amp is on its **BLUETOOTH** channel and in **[A]** mode, and that it's the outgoing COM port (Find GPA100 picks it).
- **Still stuck:** tick **Log the CI-V traffic**, try again, and attach `%APPDATA%\OpenHPSDR\Kainos-x64\KainosCivLog.txt` to a bug report (**Report a bug**, top right of Kainos).

The GPA100 doesn't send power or SWR over Bluetooth, so Kainos can't show them from the amp.
