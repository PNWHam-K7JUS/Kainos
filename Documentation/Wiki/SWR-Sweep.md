# SWR Sweep

Plot your antenna's SWR across a band, using the HL2's own power readings.

## Running a sweep

1. Press **SWR** in the left-hand column.
2. Pick a band (or **Custom** and your own range) and the number of steps (50 is quick).
3. Press **Start sweep**. A reminder appears first:
   - turn off, or bypass, any **antenna tuner**;
   - put any **amplifier** in bypass, or switch it off;
   - make sure an **antenna or dummy load** is connected.
4. Kainos steps a low-power carrier (**1 W at most**) across the band and plots SWR as it goes.

The readout shows the **lowest SWR** and where it is, and the **2:1 bandwidth**.

## Saving and comparing

- **Save** keeps the sweep. Tick earlier sweeps in the list to lay them over the current one, to compare antennas or watch one change over time.
- **Export CSV** saves a sweep for a spreadsheet. Sweeps are kept in `%APPDATA%\OpenHPSDR\Kainos-x64\Sweeps`.

## Safety

- The sweep stops by itself if SWR goes above 5:1, the output goes above 1 W, or the output drops away.
- It only steps inside the band for your region. Split must be off first, since it transmits on VFO A.
- Afterwards your frequency, tune power and tune settings are put back.
- It does transmit, briefly, on each step: be considerate of others on the band.

Only SWR is measured (the HL2's bridge doesn't give the phase needed for impedance or a Smith chart).
