#pragma once
/*******************Preprocessor Directives***************/
#include "SmartPlayer.h"

/*******************Global variables***************/
SmartPlayer* ai;

/*******************Functions***************/
extern "C"
{
	_declspec(dllexport) void Initialize(char* exePath, unsigned long numOfNewSongs);/*Initializes
	the ai*/
	_declspec(dllexport) void Train(unsigned long songIndex); 
	/*Trains the ai based ont he songs played by the user individually*/
	_declspec(dllexport) void GetFeedback(int songDuration, int durationHeard); 
	/*Gets user feedback for a song played by the ai*/
	_declspec(dllexport) unsigned long Output(); /*Returns the song to play*/
	_declspec(dllexport) void BeforeClosing(); /*Preprocessing befor the application exists*/
}

