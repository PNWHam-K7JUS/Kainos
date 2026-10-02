/*  aetherstrip.cpp

This file is part of Kainos, a program that implements a Software-Defined Radio.

The AetherSDR channel strip in WDSP: AetherSDR's own processors (wdsp/aethersdr/, from
https://github.com/aethersdr/AetherSDR, commit 20d022d5, by the AetherSDR contributors) driven
from a WDSP channel. Copyright (C) 2026 Justin Cron K7JUS.

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.

*/

#include "aetherstrip.h"
#include "aethersdr/ClientGate.h"
#include "aethersdr/ClientDeEss.h"
#include "aethersdr/ClientComp.h"
#include "aethersdr/ClientTube.h"
#include "aethersdr/ClientReverb.h"
#include "aethersdr/ClientFinalLimiter.h"
#include "aethersdr/ClientEq.h"

#include <cmath>

#include <cstring>
#include <vector>

extern "C" {
#include "aethervoice.h"
}

using namespace AetherSDR;

struct _aetherstrip
{
	int size;							// complex samples per block
	double* in;
	double* out;
	double samplerate;
	ClientGate gate;
	ClientDeEss deess;
	ClientComp comp;
	ClientTube tube;
	ClientReverb reverb;
	ClientFinalLimiter limiter;
	ClientEq eq;
	ClientEq::BandParams bands[AS_EQ_BANDS];	// the EQ bands as last set (ClientEq takes a whole band at a time)
	int order[AS_MAXORDER];				// chain order: stages and AS_EXCITER (the final limiter always runs last)
	int orderCount;
	bool wasEnabled[AS_NSTAGES];		// to reset a stage's state when it is switched on
	std::vector<float> buf;				// interleaved float scratch (AetherSDR processes float)
};

namespace
{
	// AetherSDR's default chain orders (AudioEngine TxChainStage / RxChainStage); de-essing and reverb
	// are transmit tools
	const int kTxOrder[] = { AS_GATE, AS_EQ, AS_DEESS, AS_COMP, AS_TUBE, AS_EXCITER, AS_REVERB };
	const int kRxOrder[] = { AS_EQ, AS_GATE, AS_COMP, AS_TUBE, AS_EXCITER };

	bool stageEnabled (AETHERSTRIP a, int stage)
	{
		switch (stage)
		{
		case AS_GATE:		return a->gate.isEnabled ();
		case AS_DEESS:		return a->deess.isEnabled ();
		case AS_COMP:		return a->comp.isEnabled ();
		case AS_TUBE:		return a->tube.isEnabled ();
		case AS_REVERB:		return a->reverb.isEnabled ();
		case AS_LIMITER:	return a->limiter.isEnabled ();
		case AS_EQ:			return a->eq.isEnabled ();
		}
		return false;
	}

	void stageReset (AETHERSTRIP a, int stage)
	{
		switch (stage)
		{
		case AS_GATE:		a->gate.reset (); break;
		case AS_DEESS:		a->deess.reset (); break;
		case AS_COMP:		a->comp.reset (); break;
		case AS_TUBE:		a->tube.reset (); break;
		case AS_REVERB:		a->reverb.reset (); break;
		case AS_LIMITER:	a->limiter.reset (); break;
		case AS_EQ:			a->eq.reset (); break;
		}
	}

	void stageProcess (AETHERSTRIP a, int stage, float* x, int frames, int nch)
	{
		switch (stage)
		{
		case AS_GATE:		a->gate.process (x, frames, nch); break;
		case AS_DEESS:		a->deess.process (x, frames, nch); break;
		case AS_COMP:		a->comp.process (x, frames, nch); break;
		case AS_TUBE:		a->tube.process (x, frames, nch); break;
		case AS_REVERB:		a->reverb.process (x, frames, nch); break;
		case AS_LIMITER:	a->limiter.process (x, frames, nch); break;
		case AS_EQ:			a->eq.process (x, frames, nch); break;
		}
	}

	void toFloat (AETHERSTRIP a, const double* src, float* x, int nch)
	{
		for (int i = 0; i < a->size; i++)
		{
			x[nch * i] = (float)src[2 * i];
			if (nch == 2) x[nch * i + 1] = (float)src[2 * i + 1];
		}
	}

	// I (and Q when stereo) back to doubles; mono leaves Q as it came in
	void toDouble (AETHERSTRIP a, const float* x, int nch)
	{
		for (int i = 0; i < a->size; i++)
		{
			a->out[2 * i] = x[nch * i];
			if (nch == 2) a->out[2 * i + 1] = x[nch * i + 1];
		}
	}

	void refreshEnables (AETHERSTRIP a, int s, bool& any)
	{
		bool on = stageEnabled (a, s);
		if (on && !a->wasEnabled[s]) stageReset (a, s);		// start from clean state
		a->wasEnabled[s] = on;
		any = any || on;
	}

	void prepareAll (AETHERSTRIP a)
	{
		a->gate.prepare (a->samplerate);
		a->deess.prepare (a->samplerate);
		a->comp.prepare (a->samplerate);
		a->tube.prepare (a->samplerate);
		a->reverb.prepare (a->samplerate);
		a->limiter.prepare (a->samplerate);
		a->eq.prepare (a->samplerate);
	}
}

extern "C" {

AETHERSTRIP create_aetherstrip (int size, double* in, double* out, int samplerate, int rx)
{
	AETHERSTRIP a = new _aetherstrip ();
	a->size = size;
	a->in = in;
	a->out = out;
	a->samplerate = samplerate;
	a->buf.assign (2 * size, 0.0f);
	for (int s = 0; s < AS_NSTAGES; s++) a->wasEnabled[s] = false;
	// AetherSDR's final limiter defaults to on (it guards AetherSDR's own output); in Kainos
	// every strip stage starts off until the user turns it on
	a->limiter.setEnabled (false);
	a->orderCount = rx ? (int)(sizeof (kRxOrder) / sizeof (int)) : (int)(sizeof (kTxOrder) / sizeof (int));
	std::memcpy (a->order, rx ? kRxOrder : kTxOrder, a->orderCount * sizeof (int));
	// EQ: AetherSDR's default 10-band layout, every band off and flat until shaped
	a->eq.setActiveBandCount (AS_EQ_BANDS);
	for (int b = 0; b < AS_EQ_BANDS; b++)
	{
		a->bands[b] = ClientEq::defaultBand (b);
		a->eq.setBand (b, a->bands[b]);
	}
	prepareAll (a);
	return a;
}

void destroy_aetherstrip (AETHERSTRIP a)
{
	delete a;
}

void flush_aetherstrip (AETHERSTRIP a)
{
	for (int s = 0; s < AS_NSTAGES; s++) stageReset (a, s);
}

void xaetherstrip (AETHERSTRIP a, int stereo, struct _aethervoice* av)
{
	int nch = stereo ? 2 : 1, k;
	bool any = false;
	for (k = 0; k < a->orderCount; k++)
		if (a->order[k] != AS_EXCITER) refreshEnables (a, a->order[k], any);
	refreshEnables (a, AS_LIMITER, any);

	if (!any)
	{
		// no strip stage on: just the exciter (bit-exact pass-through when it is off too)
		if (a->in != a->out) std::memcpy (a->out, a->in, a->size * 2 * sizeof (double));
		if (av) xaethervoice (av, stereo);
		return;
	}

	float* x = a->buf.data ();
	toFloat (a, a->in, x, nch);
	for (k = 0; k < a->orderCount; k++)
	{
		int s = a->order[k];
		if (s == AS_EXCITER)
		{
			// AetherVoice works on the WDSP buffer in place (double), so hand the audio over at its slot
			if (av && av->run)
			{
				toDouble (a, x, nch);
				xaethervoice (av, stereo);
				toFloat (a, a->out, x, nch);
			}
		}
		else if (a->wasEnabled[s]) stageProcess (a, s, x, a->size, nch);
	}
	if (a->wasEnabled[AS_LIMITER]) stageProcess (a, AS_LIMITER, x, a->size, nch);
	if (a->in != a->out && !stereo)
		for (int i = 0; i < a->size; i++) a->out[2 * i + 1] = a->in[2 * i + 1];
	toDouble (a, x, nch);
}

int setOrder_aetherstrip (AETHERSTRIP a, const int* order, int n)
{
	bool seen[AS_MAXORDER] = { false };
	if (n < 1 || n > AS_MAXORDER) return 0;
	for (int k = 0; k < n; k++)
	{
		int s = order[k];
		if (s < 0 || s >= AS_MAXORDER || s == AS_LIMITER || seen[s]) return 0;
		seen[s] = true;
	}
	std::memcpy (a->order, order, n * sizeof (int));
	a->orderCount = n;
	return 1;
}

void setBuffers_aetherstrip (AETHERSTRIP a, double* in, double* out)
{
	a->in = in;
	a->out = out;
}

// called with the channel stopped (SetDSPSamplerate / SetDSPBuffsize), so prepare() may allocate
void setSamplerate_aetherstrip (AETHERSTRIP a, int rate)
{
	a->samplerate = rate;
	prepareAll (a);
}

void setSize_aetherstrip (AETHERSTRIP a, int size)
{
	a->size = size;
	a->buf.assign (2 * size, 0.0f);
}

// parameter setters are lock-free in AetherSDR (atomics, applied at the next block)
void setParam_aetherstrip (AETHERSTRIP a, int stage, int param, double value)
{
	float v = (float)value;
	bool on = value != 0.0;
	int n = (int)(value + (value < 0 ? -0.5 : 0.5));
	switch (stage)
	{
	case AS_GATE:
		switch (param)
		{
		case 0: a->gate.setEnabled (on); break;
		case 1: a->gate.setMode (n == 1 ? ClientGate::Mode::Gate : ClientGate::Mode::Expander); break;
		case 2: a->gate.setThresholdDb (v); break;
		case 3: a->gate.setRatio (v); break;
		case 4: a->gate.setAttackMs (v); break;
		case 5: a->gate.setReleaseMs (v); break;
		case 6: a->gate.setHoldMs (v); break;
		case 7: a->gate.setFloorDb (v); break;
		case 8: a->gate.setReturnDb (v); break;
		case 9: a->gate.setLookaheadMs (v); break;
		}
		break;
	case AS_DEESS:
		switch (param)
		{
		case 0: a->deess.setEnabled (on); break;
		case 1: a->deess.setFrequencyHz (v); break;
		case 2: a->deess.setQ (v); break;
		case 3: a->deess.setThresholdDb (v); break;
		case 4: a->deess.setAmountDb (v); break;
		case 5: a->deess.setAttackMs (v); break;
		case 6: a->deess.setReleaseMs (v); break;
		case 7: a->deess.setSlopeStages (n); break;
		}
		break;
	case AS_COMP:
		switch (param)
		{
		case 0: a->comp.setEnabled (on); break;
		case 1: a->comp.setThresholdDb (v); break;
		case 2: a->comp.setRatio (v); break;
		case 3: a->comp.setAttackMs (v); break;
		case 4: a->comp.setReleaseMs (v); break;
		case 5: a->comp.setKneeDb (v); break;
		case 6: a->comp.setMakeupDb (v); break;
		case 7: a->comp.setLimiterEnabled (on); break;
		case 8: a->comp.setLimiterCeilingDb (v); break;
		case 9: a->comp.setDriveDb (v); break;
		case 10: a->comp.setPhaseRotatorStages (n); break;
		}
		break;
	case AS_TUBE:
		switch (param)
		{
		case 0: a->tube.setEnabled (on); break;
		case 1: a->tube.setModel (n == 2 ? ClientTube::Model::C : n == 1 ? ClientTube::Model::B : ClientTube::Model::A); break;
		case 2: a->tube.setDriveDb (v); break;
		case 3: a->tube.setBiasAmount (v); break;
		case 4: a->tube.setTone (v); break;
		case 5: a->tube.setOutputGainDb (v); break;
		case 6: a->tube.setDryWet (v); break;
		case 7: a->tube.setEnvelopeAmount (v); break;
		case 8: a->tube.setReleaseMs (v); break;
		}
		break;
	case AS_REVERB:
		switch (param)
		{
		case 0: a->reverb.setEnabled (on); break;
		case 1: a->reverb.setSize (v); break;
		case 2: a->reverb.setDecayS (v); break;
		case 3: a->reverb.setDamping (v); break;
		case 4: a->reverb.setPreDelayMs (v); break;
		case 5: a->reverb.setMix (v); break;
		}
		break;
	case AS_LIMITER:
		switch (param)
		{
		case 0: a->limiter.setEnabled (on); break;
		case 1: a->limiter.setCeilingDb (v); break;
		case 2: a->limiter.setOutputTrimDb (v); break;
		case 3: a->limiter.setDcBlockEnabled (on); break;
		}
		break;
	case AS_EQ:
		if (param == 0) a->eq.setEnabled (on);
		else if (param == 1) a->eq.setMasterGain ((float)std::pow (10.0, value / 20.0));
		else if (param == 2) a->eq.setFilterFamily ((ClientEq::FilterFamily)(n < 0 ? 0 : n > 3 ? 3 : n));
		else if (param >= AS_EQ_BAND0 && param < AS_EQ_BAND0 + 6 * AS_EQ_BANDS)
		{
			int b = (param - AS_EQ_BAND0) / 6;
			ClientEq::BandParams& p = a->bands[b];
			switch ((param - AS_EQ_BAND0) % 6)
			{
			case 0: p.freqHz = v; break;
			case 1: p.gainDb = v; break;
			case 2: p.q = v; break;
			case 3: p.type = (ClientEq::FilterType)(n < 0 ? 0 : n > 4 ? 4 : n); break;
			case 4: p.enabled = on; break;
			case 5: p.slopeDbPerOct = n; break;
			}
			a->eq.setBand (b, p);
		}
		break;
	}
}

double aetherstrip_eqBandMagnitudeDb (int type, double freq, double gain, double q, int on, int slope,
	int family, double probeHz, double samplerate)
{
	ClientEq::BandParams p;
	p.type = (ClientEq::FilterType)(type < 0 ? 0 : type > 4 ? 4 : type);
	p.freqHz = (float)freq;
	p.gainDb = (float)gain;
	p.q = (float)q;
	p.enabled = on != 0;
	p.slopeDbPerOct = slope;
	return ClientEq::bandMagnitudeDb (p, (float)probeHz, samplerate, (ClientEq::FilterFamily)(family < 0 ? 0 : family > 3 ? 3 : family));
}

double getMeter_aetherstrip (AETHERSTRIP a, int stage, int meter)
{
	switch (stage)
	{
	case AS_GATE:
		if (meter == 0) return a->gate.gainReductionDb ();
		if (meter == 10) return a->gate.inputPeakDb ();
		if (meter == 11) return a->gate.outputPeakDb ();
		break;
	case AS_DEESS:
		if (meter == 0) return a->deess.gainReductionDb ();
		if (meter == 10) return a->deess.inputPeakDb ();
		break;
	case AS_COMP:
		if (meter == 0) return a->comp.gainReductionDb ();
		if (meter == 1) return a->comp.limiterGrDb ();
		if (meter == 10) return a->comp.inputPeakDb ();
		if (meter == 11) return a->comp.outputPeakDb ();
		break;
	case AS_TUBE:
		if (meter == 0) return a->tube.driveAppliedDb ();
		if (meter == 10) return a->tube.inputPeakDb ();
		if (meter == 11) return a->tube.outputPeakDb ();
		break;
	case AS_REVERB:
		if (meter == 0) return a->reverb.wetRmsDb ();
		if (meter == 10) return a->reverb.inputPeakDb ();
		if (meter == 11) return a->reverb.outputPeakDb ();
		break;
	case AS_LIMITER:
		if (meter == 0) return a->limiter.gainReductionDb ();
		if (meter == 1) return a->limiter.outputPeakDb ();
		if (meter == 2) return a->limiter.outputRmsDb ();
		if (meter == 10) return a->limiter.inputPeakDb ();
		if (meter == 11) return a->limiter.outputPeakDb ();
		break;
	}
	return -120.0;
}

}
