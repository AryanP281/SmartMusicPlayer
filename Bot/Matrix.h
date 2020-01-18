#pragma once
/************Preprocessor Directives*************/
#include <vector>

/************Classes And Struct*************/
<typename T>
struct Matrix
{
	private std::vector<std::vector<T>> data;

	T At(int row, int column)
	{
		if (row < data.size() && column < data[row].size())
		{
			return data[row][column];
		}

		return NULL;
	}
};
