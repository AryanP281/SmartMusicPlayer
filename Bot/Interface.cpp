/*******************Preprocessor Directives***************/
#include "stdafx.h"
#include "Interface.h"

/*******************Functions***************/
void Initialize(char* exePath, unsigned long numOfNewSongs)
{
	//Initialiazing the ai
	ai = new SmartPlayer(numOfNewSongs, exePath);
}

void Train(unsigned long songIndex)
{
	ai->Train(songIndex);
}

void GetFeedback(int songDuration, int durationHeard)
{
	ai->GetFeedback(songDuration, durationHeard);
}

unsigned long Output()
{
	return ai->Output();
}

void BeforeClosing()
{
	//Deallocating the memory for the ai
	delete ai;
}
