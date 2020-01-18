#pragma once
/******************Preprocessor Directives***************/
#include <vector>

using namespace std;

/******************Structs***************/
namespace Misc
{
	template<typename T> struct Matrix
	{
	private:
		vector<vector<T>> data;

	public:
		Matrix()
		{
		}

		T& At(unsigned long column, unsigned long row)
		{
			return data[column][row];
		}

		void AddColumn(const vector<T>& columnData = vector<T>())
		{
			data.push_back(columnData);
		}

		void AddRow(const vector<T>& rowData)
		{
			for (unsigned long a = 0; a < data.size(); ++a)
			{
				data[a].push_back(rowData[a]);
			}
		}

		vector<T>& Column(unsigned long column)
		{
			return data[column];
		}

		vector<T*> Row(unsigned long rowNum)
		{
			vector<T*> row = vector<T*>();

			for (unsigned long x = 0; x < data.size(); ++x)
			{
				row.push_back(&(data[x][rowNum]));
			}

			return row;
		}

		int NumOfRows() const
		{
			//Returns the max number of rows
			unsigned long maxRowSize = 0;
			for (unsigned long a = 0; a < data.size(); ++a)
			{
				if (data[a].size() > maxRowSize)
					maxRowSize = data[a].size();
			}

			return maxRowSize;
		}
		int NumOfColumns() const
		{
			return data.size();
		}

	};

	/******************Functions**************************/
	std::string SubString(const std::string& str, int startPos, char charEncountered);
}
