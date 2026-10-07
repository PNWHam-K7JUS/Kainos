# Using the HL2 I/O board's antennas in Kainos

The Hermes Lite 2 **I/O board** adds antenna switching to the HL2. In Kainos (as in MI0BOT's HL2 edition of Thetis, which Kainos is built on) you can use it to:

- receive on a separate **receive-only antenna** (the I/O board's RX input, called **ALT RX** in Setup), for example a quiet loop or Beverage, while still transmitting on your main antenna;
- choose the receive and transmit antenna **for each band**.

## One thing to know first: the HL2 has one receiver input

The HL2 has a single receiver input (one ADC). **RX1 and RX2 always listen through the same antenna**: whichever antenna is selected for receive. When the I/O board switches to the receive-only antenna, both receivers move to it.

So you can't put RX2 on a different antenna from RX1 at the same time. That would need a second receiver input, which the HL2 doesn't have.

## 1. Tell Kainos you have the I/O board

Do either of these:

- Run the **setup wizard** (Setup > Appearance > Kainos > **Run setup wizard**), and tick **HL2 I/O board** on the Hardware page.
- Or tick **HL2 I/O Board** in **Setup > General > H/W Select**, under Hardware Options.

## 2. Choose the antennas for each band

Open **Setup > General > Ant/Filters > Antenna**. The **Antenna Control** box has a row for each band.

**Receive** (left side):

| Column | What it does |
|---|---|
| **1 / 2 / 3** | The antenna port used for receive on that band. |
| **ALT RX** | Receive on the I/O board's **receive-only input** on that band. Transmit still uses the antenna chosen under Transmit. |
| **RX2** | Not used by the I/O board in this version. Use **ALT RX** for the I/O board's receive-only input. |
| **XVTR** | For transverters (see below). |

**Transmit** (right side):

| Column | What it does |
|---|---|
| **1 / 2 / 3** | The antenna port used for transmit on that band. |
| **Do Not TX** | Under antennas 2 and 3: tick to stop Kainos transmitting on that antenna, for example when it's a receive-only antenna. |

Press **OK** or **Apply**. The I/O board switches as you change bands.

### Example: a receive loop on 80 m and 160 m

1. Connect the loop to the I/O board's RX input.
2. In **Antenna Control**, tick **ALT RX** on the **160m** and **80m** rows.
3. Leave the **Transmit** antenna on **1** for those bands.

On 160 m and 80 m Kainos now listens on the loop and switches back to antenna 1 when you transmit. On every other band it listens on antenna 1 as before.

## 3. Switching quickly

The **Rx Ant** button, in the right-hand column's **VFO SYNC** tab, switches the receive-only antenna on and off without going into Setup. It's handy for comparing the loop with your main antenna on the same signal.

## Transverters

If a transverter (Setup > XVTRs) is set to receive on antenna **4**, Kainos assumes an I/O board and switches to its receive-only input for that transverter automatically. **Rx Ant** is greyed out while you're on it, since switching wouldn't make sense there.

## If it doesn't switch

- Check **HL2 I/O Board** is ticked (step 1), and the radio is on.
- Check the I/O board's firmware is up to date (see the HL2 wiki).
- Make sure the band you're on has **ALT RX** ticked: the setting is per band.
- Still stuck? Use **Report a bug** (top right of Kainos) and say which band, which boxes are ticked, and what you hear.

---

*Kainos inherits its I/O board support from MI0BOT's HL2 edition of Thetis. Thanks, Reid!*
