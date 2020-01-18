/**************************Preprocessor Directives***************/
#include "stdafx.h"
#include "Buffer.h"

/**************************Constructors And Destructors***************/
Buffer::Buffer() : MAX_SIZE(0)
{
	this->buffer = new char[1];
	this->currentSize = 0;
}

Buffer::Buffer(int bufferSize) : MAX_SIZE(bufferSize)
{
	this->buffer = new char[MAX_SIZE];
	this->currentSize = 0;
}

Buffer::~Buffer()
{
	delete[] buffer;
}

/**************************Methods***************/
const char* Buffer::BufferData() const
{
	return buffer;
}

void Buffer::Add(char* newData, int newDataSize)
{
	if (currentSize + newDataSize > MAX_SIZE)
		throw "Unable to add data to buffer. Buffer limit reached";

	for (int a = 0; a < newDataSize; ++a, ++currentSize)
	{
		buffer[currentSize] = newData[a];
	}
}

void Buffer::Flush(std::fstream& file)
{
	file.write(buffer, currentSize);

	currentSize = 0;
}

bool Buffer::IsFull() const
{
	if (currentSize == MAX_SIZE)
		return true;

	return false;
}

int Buffer::CurrentSize() const
{
	return currentSize;
}
