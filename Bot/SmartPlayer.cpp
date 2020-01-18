/*******************Preprocessor Directives*************/
#include "stdafx.h"
#include <time.h>
#include <fstream>
#include <random>
#include <ctime>
#include <Windows.h>
#include "SmartPlayer.h"


/*******************Constructors And Destructors*************/
SmartPlayer::SmartPlayer(unsigned long numOfNewSongs, std::string exePath) 
: FILE_ADDR(exePath + "\\DecisionMatrix.dat"), PRIMARY_LEARNING_RATE(0.3f), PRIMARY_FORGETTING_RATE(0.1f), 
SECONDARY_LEARNING_RATE(0.15f), SECONDARY_FORGETTING_RATE(0.05f),
RANDOMNESS(0.25F), TIME_STEP(24 * 2), PROGRAM_JUST_STARTED_STATE(0), numNewSongs(numOfNewSongs),
decisionMatrixDb(FILE_ADDR)
{	
	//Initializing the decision matrix
	void (SmartPlayer::*initializationFuncPtr)(unsigned long*) = &SmartPlayer::Initialize;
	parallelThread = std::thread(&SmartPlayer::RunOnParallelThreads, this, initializationFuncPtr, &numNewSongs);

	//Initializing the last
	lastState = PROGRAM_JUST_STARTED_STATE;
}

SmartPlayer::~SmartPlayer()
{
	//Joining the parallel thread
	if (parallelThread.joinable())
		parallelThread.join();

}

/*******************Private Functions*************/
void SmartPlayer::RunOnParallelThreads(void (SmartPlayer::*func)(unsigned long*), unsigned long* args)
{
	//Enabling a lock on the resources to prevent race condition
	std::lock_guard<std::mutex> lock(this->mutex);
	
	//Executing the function
	(this->*func)(args);
}

void SmartPlayer::Initialize(unsigned long* numNewSongs)
{
	if (decisionMatrixDb.Rows() == 0) //Checking if this is the first time that the program is run
	{
		decisionMatrixDb.Initialize(FILE_ADDR, *numNewSongs + 1, *numNewSongs, sizeof(float) * TIME_STEP);
		InitializeBrain(*numNewSongs + 1, *numNewSongs); //An extra row for state 0
	}
	else
	{
		decisionMatrixDb.Initialize(FILE_ADDR, *numNewSongs, *numNewSongs, sizeof(float) * TIME_STEP);
		InitializeBrain(*numNewSongs, *numNewSongs);
	}

}

void SmartPlayer::InitializeBrain(unsigned long newRows, unsigned long newCols)
{
	unsigned long rows = decisionMatrixDb.Rows();
	unsigned long cols = decisionMatrixDb.Columns();
	//The number of elements to add to the database
	unsigned long numOfElements = (newRows * cols) + (newCols * (rows - newRows));
	
	/*An array for the randomly initialized values*/
	float* data = new float[(newRows * cols * TIME_STEP) + (newCols * (rows - newRows) * TIME_STEP)];
	/*An array for the coordinates of the new songs*/
	std::pair<unsigned long, unsigned long>* coords = new std::pair<unsigned long, unsigned long>[numOfElements];

	unsigned long ctr = 0; //The current positions in the arrays
	//Adding the new rows
	srand(time(NULL));
	for (unsigned long y = rows - newRows; y < rows; ++y)
	{
		for (unsigned long x = 0; x < cols; ++x)
		{
			coords[ctr / TIME_STEP] = std::pair<unsigned long, unsigned long>(y, x);
			for (char a = 0; a < TIME_STEP; ++a, ++ctr)
			{
				float random = (float)(rand() % 100) / 100.0;
				data[ctr] = random;
			}
			
		}
	}

	//Adding the new columns to the already existing rows
	for (unsigned long y = 0; y < rows - newRows; ++y)
	{
		for (unsigned long x = cols - newCols; x < cols; ++x)
		{
			coords[ctr / TIME_STEP] = std::pair<unsigned long, unsigned long>(y, x);
			for (char a = 0; a < TIME_STEP; ++a, ++ctr)
			{
				float random = (float)(rand() % 100) / 100.0;
				data[ctr] = random;
			}
		}
	}

	//Adding the data to the database
	decisionMatrixDb.Add(data, coords, numOfElements);

	delete[] data;
	delete[] coords;
}

void SmartPlayer::ConvertVectorToArray(float* arr, const std::vector<float>& vec)
{
	for (int a = 0; a < vec.size(); ++a)
	{
		arr[a] = vec[a];
	}
}

void SmartPlayer::Reinforce(unsigned long row, unsigned long toEnforce)
{
	for (int col = 0; col < decisionMatrixDb.Columns(); ++col)
	{
		Key coords = Key(row, col);

		std::vector<float>* valuesVector = new std::vector<float>;
		*valuesVector = decisionMatrixDb.Get(&coords, 1);
		float* vals = new float[valuesVector->size()]; /*The current
		 value for the song*/
		ConvertVectorToArray(vals, *valuesVector);
		delete valuesVector;

		if (col == toEnforce)
		{
			for (char t = 0; t < TIME_STEP; ++t)
			{
				if (IsCurrentTimeStep(t + 1))
				{
					vals[t] += PRIMARY_LEARNING_RATE * (1.0 - vals[t]);
				}
				else
				{
					vals[t] += SECONDARY_LEARNING_RATE * (1.0 - vals[t]);
				}
			}
		}
		else
		{
			for (char t = 0; t < TIME_STEP; ++t)
			{
				if (IsCurrentTimeStep(t + 1))
				{
					vals[t] += PRIMARY_FORGETTING_RATE * (0.0 - vals[t]);
				}
				else
				{
					vals[t] += SECONDARY_FORGETTING_RATE * (0.0 - vals[t]);
				}
			}
		}

		decisionMatrixDb.Edit(vals, &coords);
		delete[] vals;
	}
}

void SmartPlayer::Reinforce(unsigned long row, unsigned long toEnforce, char timeStep)
{
	for (unsigned long col = 0; col < decisionMatrixDb.Columns(); ++col)
	{
		Key coords = Key(row, col);

		std::vector<float>* valuesVector = new std::vector<float>;
		*valuesVector = decisionMatrixDb.Get(&coords, 1);
		float* vals = new float[valuesVector->size()]; /*The current
		value for the song*/
		ConvertVectorToArray(vals, *valuesVector);
		delete valuesVector;

		for (char t = 0; t < TIME_STEP; ++t)
		{
			if (col == toEnforce)
			{
				if (t == timeStep)
				{
					vals[t] += PRIMARY_LEARNING_RATE * (1.0 - vals[t]);
					decisionMatrixDb.Edit(vals, &coords);
				}
				else
				{
					vals[t] += SECONDARY_LEARNING_RATE * (1.0 - vals[t]);
					decisionMatrixDb.Edit(vals, &coords);
				}
			}
			else
			{
				if (t == timeStep)
				{
					vals[t] = PRIMARY_FORGETTING_RATE * (0.0 - vals[t]);
					decisionMatrixDb.Edit(vals, &coords);
				}
				else
				{
					vals[t] = SECONDARY_FORGETTING_RATE * (0.0 - vals[t]);
					decisionMatrixDb.Edit(vals, &coords);
				}
			}
		}
	}
}

bool SmartPlayer::IsCurrentTimeStep(char t) const
{
	time_t now; //The current time
	struct tm* tm = new struct tm;

	time(&now); //Getting the time
	localtime_s(tm, &now);

	short totalMins = (tm->tm_hour * 60) + tm->tm_min; /*The total minutes that
	have passed since midnight i.e 12:00 am*/

	delete tm;

	if (std::ceil(totalMins / 30) == t)
		return true;

	return false;
}

char SmartPlayer::CurrentTimeStep() const
{
	time_t now; //The current time
	struct tm* tm = new struct tm;

	time(&now); //Getting the time
	localtime_s(tm, &now);

	char currentTimeStep = std::ceil((tm->tm_hour * 60) + tm->tm_min);

	delete tm;
	return currentTimeStep;
}

void SmartPlayer::GetNextSong(unsigned long*)
{
	unsigned long nextSong = 0;
	float lastGreatestVal = 0.0;
	unsigned long totalColsInDecisionMatrix = decisionMatrixDb.Columns();
	char currentTimeStep = CurrentTimeStep();

	for (unsigned long col = 0; col < totalColsInDecisionMatrix; ++col)
	{
		Key coords = Key(lastState, col);
		float matrixVal = decisionMatrixDb.Get(&coords)[currentTimeStep];

		if (matrixVal > lastGreatestVal)
			nextSong = col;
	}

	lastState = currentState; //Setting the current state as the last state
	currentState = nextSong;
	nextSongRetrieved = true;
}

void SmartPlayer::Quit()
{
	
}

/*******************Public Methods*************/
void SmartPlayer::Train(unsigned long songId)
{
	//Enabling a lock on the resources to prevent race condition
	std::lock_guard<std::mutex> lock(this->mutex);
	
	//Reinforcing the behaviour
	Reinforce(lastState, songId);

	//Updating the last state
	lastState = songId;
}

unsigned long SmartPlayer::Output()
{
	//Checking if the next song to play has already been retrieved
	if (nextSongRetrieved)
	{
		nextSongRetrieved = false;
		unsigned long currentStateCopy = currentState; //A copy of the current state to prevent overwriting by parallel thread
		if (parallelThread.joinable())
		{
			void (SmartPlayer::*nextSongFuncPtr)(unsigned long*) = &SmartPlayer::GetNextSong;
			parallelThread = std::thread(&SmartPlayer::RunOnParallelThreads, this, nextSongFuncPtr, nullptr);
		}
		return currentStateCopy;
	}
	
	srand(time(NULL));
	char random = rand() % 101; //Determining whether to play a random song
	
	if(random <= (RANDOMNESS * 100)) //Playing a random song based on probabilities
	{ 
		lastState = currentState; //Setting the current state as the last state
		currentState = rand() % (decisionMatrixDb.Columns() + 1);//Setting the new state as the current state
		return (currentState);
	}
	else
	{
		//Getting the next song
		GetNextSong(nullptr);
	}

	if (parallelThread.joinable())
	{
		nextSongRetrieved = false;

		void (SmartPlayer::*nextSongFuncPtr)(unsigned long*) = &SmartPlayer::GetNextSong;
		parallelThread = std::thread(&SmartPlayer::RunOnParallelThreads, this, nextSongFuncPtr, nullptr);
	}

	return currentState;
}

void SmartPlayer::GetFeedback(int songDuration, int durationHeard)
{
	float learningRateModifier = durationHeard / songDuration;

	//Enabling a lock on the resources to prevent race condition
	std::lock_guard<std::mutex> lock(this->mutex);

	//Getting the matrix data
	Key coords = Key(lastState, currentState);
	std::vector<float>* valuesVector = new std::vector<float>;
	*valuesVector = decisionMatrixDb.Get(&coords, 1);
	float* vals = new float[valuesVector->size()]; /*The current
	value for the song*/
	ConvertVectorToArray(vals, *valuesVector);
	delete valuesVector;

	if (durationHeard >= (songDuration / 2))
	{
		for (char t = 0; t < TIME_STEP; ++t)
		{
			if (IsCurrentTimeStep(t + 1))
			{
				vals[t] += (learningRateModifier * (1.0 - vals[t]));
				decisionMatrixDb.Edit(vals, &coords);
			}
			else
			{
				vals[t] += ((SECONDARY_LEARNING_RATE / PRIMARY_LEARNING_RATE) * 
				learningRateModifier * (1.0 - vals[t]));

				decisionMatrixDb.Edit(vals, &coords);
			}
		}
	}
	else
	{
		for (char t = 0; t < TIME_STEP; ++t)
		{
			if (IsCurrentTimeStep(t + 1))
			{
				vals[t] += learningRateModifier * (0.0 - vals[t]);
				decisionMatrixDb.Edit(vals, &coords);
			}
			else
			{
				vals[t] += (SECONDARY_FORGETTING_RATE / PRIMARY_FORGETTING_RATE) 
				* learningRateModifier * (0.0 - vals[t]);

				decisionMatrixDb.Edit(vals, &coords);
			}
		}
	}
}

void SmartPlayer::ForgetSong(unsigned long songId)
{
	//Enabling a lock on the resources to prevent race condition
	std::lock_guard<std::mutex> lock(this->mutex);

	//Deleting the song from the database
	
}
