#pragma once

/**************************Preprocessor Directives***************/
#include <fstream>

/**************************Classes***************/
class Buffer
{
private:
	const int MAX_SIZE;
	int currentSize;
	char* buffer;

public:
	Buffer();
	Buffer(int bufferSize);
	~Buffer();

	const char* BufferData() const;
	void Add(char* newData, int newDataSize);
	void Flush(std::fstream& file);
	bool IsFull() const;
	int CurrentSize() const;
};
