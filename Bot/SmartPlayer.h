#pragma once
/******************Preprocessor Directives***************/
#include <vector>
#include <string>
#include <thread>
#include <mutex>
#include "Misc.h"
#include "MatrixDatabase.h"

/*******************Typedef*************/
typedef std::pair<int, std::string> MusicFile; /*The index 
and address of the music file*/
typedef std::pair<std::thread, bool> Thread; /*The thread object and a variable
for telling if if it has completed is execution*/

/******************Classes***************/
class SmartPlayer
{
private:
	//Private Variables
	const char PROGRAM_JUST_STARTED_STATE; /*The state id when no song has been
	played before*/
	const std::string FILE_ADDR; //The address of the file containing the initializing data
	const char TIME_STEP; /*The smallest time interval that the bot can work with*/
	const float PRIMARY_LEARNING_RATE; //The learning rate for enforcing the actions in the timestep
	const float PRIMARY_FORGETTING_RATE; //The rate at which the reward for songs is reduced in the timestep
	const float SECONDARY_LEARNING_RATE; //The learning rate for enforcing the actions outside the timestep
	const float SECONDARY_FORGETTING_RATE; //The rate at which the reward for songs is reduced outside the timestep
	const float RANDOMNESS; /*The probability of a random song being played. An
	explorative movie*/
	unsigned long numNewSongs; //The number of new songs that have been detected
	MatrixDatabase<float> decisionMatrixDb; /*The matrix of the ai stored in a
	file on disc*/
	unsigned long currentState; /*The current state i.e the song which has been played last*/
	unsigned long lastState; /*The last state i.e the song which was played before this*/
	std::thread parallelThread; /*A thread for parallel operations */
	std::mutex mutex; //A mutex lock for preventing race conditions
	bool nextSongRetrieved; /*Tells whether the next song has already been retrived*/

	//Private Functions
	void RunOnParallelThreads(void (SmartPlayer::*func)(unsigned long*), unsigned long* args); /*A function
	which manages parallel threads*/
	void Initialize(unsigned long* numNewSongs); //Initializes the decision matrix
	void InitializeBrain(unsigned long newRows, unsigned long newCols); /*Initializes the decision
	matrix with the values stored*/
	void ConvertVectorToArray(float* arr, const std::vector<float>& vec);
	void Reinforce(unsigned long row, unsigned long toEnforce);
	void Reinforce(unsigned long row, unsigned long toEnforce, char timeStep);
	bool IsCurrentTimeStep(char t) const; /*Tells whether current time belongs to the
	provided time step*/
	char CurrentTimeStep() const; //Returns the current time as a time step
	void GetNextSong(unsigned long*); /*Returns the index of the next song
	to be played*/
	void Quit(); /*Performs the actions to be performed before quitting*/

public:
	//Constructors And Destructors
	SmartPlayer(unsigned long numOfNewSongs, std::string exePath);
	~SmartPlayer();

	//Methods
	void Train(unsigned long songId); /*Trains the bot based on the songs
	played by the user*/
	unsigned long Output(); //Returns the index of the next song to play
	void GetFeedback(int songDuration, int durationHeard); /*Learning from
	the user's feedback (both parameters are in milliseconds)*/
	void ForgetSong(unsigned long songId); /*Removes the particular song from
	decision matrix*/
};
