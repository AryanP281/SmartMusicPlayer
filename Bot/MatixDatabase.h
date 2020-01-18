#pragma once
/******************Preprocessor Directives***************/
#include <fstream>
#include "Buffer.h"
#include "BTree.h"

/******************Classes***************/
template<typename T> class MatrixDatabase
{
private:
	const short BTREE_ORDER; //The order of the B-tree
	int MEM_BLOCK_SIZE; //The size(in bytes) of one block of data stored in database
	bool checkedIfDbExists; //Tells whether the existence of the database file has been verified
	std::fstream db; //The binary database file
	std::string dbPath; //The path to the binary db file
	unsigned long rows; //The num of rows in the matrix
	unsigned long columns;  //The num of rows in the matrix
	BTree bTree; /*A b-tree for making the process of looking up data quicker*/

	//Private Functions
	void CheckIfDbExists(const std::string& path);/*Checks whether the database
	file exists and if not creates a new file and initializes it*/
	void CreateSpace(unsigned long newRow, unsigned long newCol); /*Creates space for the
	new elements and intitialiazes their value to 0*/
	void GenerateBTree(); //Generates the btree
	unsigned long GetPos(Key& coords); /*Returns the offset of the given coordinates
	from the start of the file*/

public:
	//Constructors And Destructors
	MatrixDatabase();
	MatrixDatabase(std::string path);
	MatrixDatabase(std::string path, unsigned long numOfRows, unsigned long numOfCols, int memBlockSize, bool createSpace = false);  
	~MatrixDatabase();

	//Methods 
	void Initialize(std::string path, unsigned long numOfRows, unsigned long numOfCols, int memBlockSize, bool createSpace = false); /*Initializes
	the class. Same as constructor*/
	std::vector<T> Get(Key* coords, unsigned long count = 1); /*Returns the element
	at the specified coordinates*/
	void Edit(T* data, Key* coords, unsigned long count = 1); /*Adds elements
	at the specified location*/
	void Add(T* data, Key* coords,unsigned long count = 1); /*Adds data to the database*/
	unsigned long Rows() const; //Returns the number of rows in the database
	unsigned long Columns() const; //Returns the number of columns in the database

	//Operators
	void operator=(const MatrixDatabase<T>& db);
};

/******************Constructors And Destructors***************/
template <typename T>
MatrixDatabase<T>::MatrixDatabase() : BTREE_ORDER(512), bTree(BTREE_ORDER)
{
	this->dbPath = "";
	this->rows = 0;
	this->columns = 0;
	this->MEM_BLOCK_SIZE = 0;
	this->checkedIfDbExists = false;
}

template <typename T>
MatrixDatabase<T>::MatrixDatabase(std::string path) : BTREE_ORDER(512), bTree(BTREE_ORDER)
{
	this->dbPath = path;
	this->rows = 0;
	this->columns = 0;
	this->MEM_BLOCK_SIZE = 0;

	//Checking if the database file exists
	CheckIfDbExists(path);

	this->checkedIfDbExists = true;
}

template <typename T>
MatrixDatabase<T>::MatrixDatabase(std::string path, unsigned long numOfRows, unsigned long numOfCols, int memBlockSize, bool createSpace) : MEM_BLOCK_SIZE(memBlockSize),
BTREE_ORDER(1048)
{
	this->Initialize(path, numOfRows, numOfCols, createSpace); //Initializing
}

template<typename T>
MatrixDatabase<T>::~MatrixDatabase()
{
}

/******************Private Functions***************/
template<typename T>
void MatrixDatabase<T>::CheckIfDbExists(const std::string& path)
{
	this->db.open(path, ios::binary | ios::in | ios::out); //Checking whether the file already exists
	if (!db.is_open()) //File does not exist
	{
		this->db.open(path, ios::binary | ios::out | ios::trunc);

		db.write(reinterpret_cast<const char*>(&rows), sizeof(rows)); /*Storing
		the number of rows present in the matrix*/
		db.write(reinterpret_cast<const char*>(&columns), sizeof(columns));  /*Storing
		the number of columns present in the matrix*/
	}
	else
	{
		char* buffer = new char[sizeof(unsigned long) * 2];
		db.seekg(-1 * (int)sizeof(unsigned long) * 2, ios::end);
		db.read(buffer, sizeof(unsigned long) * 2); //Getting the number of rows and columns the database already has

		unsigned long* data = reinterpret_cast<unsigned long*>(buffer);
		rows += data[0];
		columns += data[1];

		delete[] buffer;
	}
	db.close();
}

template<typename T>
void MatrixDatabase<T>::CreateSpace(unsigned long newRow, unsigned long newCol)
{
	db.open(dbPath, ios::in | ios::out | ios::binary);

	if (db.is_open())
	{
		//Adding the data initialized to zero value for each byte
		char* def = new char[sizeof(T)];
		for (char a = 0; a < sizeof(T); ++a)
		{
			def[a] = 0;
		}

		int offset = -1 * (int)sizeof(rows) * 2;
		db.seekp(offset, ios::end);

		//Adding the rows
		for (unsigned long y = newRow; y < rows; ++y)
		{
			for (unsigned long x = 0; x < columns; ++x)
			{
				db.write(reinterpret_cast<const char*>(&y), sizeof(y)); //Adding the row number
				db.write(reinterpret_cast<const char*>(&x), sizeof(x)); //Adding the column number
				db.write(def, sizeof(T));
			}
		}

		//Adding the columns
		for (unsigned long x = newCol; x < columns; ++x)
		{
			for (unsigned long y = 0; y < rows; ++y)
			{
				db.write(reinterpret_cast<const char*>(&y), sizeof(y)); //Adding the row number
				db.write(reinterpret_cast<const char*>(&x), sizeof(x)); //Adding the column number
				db.write(def, sizeof(T));
			}
		}
		delete[] def;

		//Saving the number of columns and rows
		db.write(reinterpret_cast<const char*>(&rows), sizeof(rows));
		db.write(reinterpret_cast<const char*>(&columns), sizeof(columns));
	}
	else
		throw ("Unable to open file " + dbPath);
}

template<typename T>
void MatrixDatabase<T>::GenerateBTree()
{
	db.open(dbPath, ios::in | ios::out | ios::binary);
	if (db.is_open())
	{
		const short COORD_DATA_SIZE = sizeof(rows) + sizeof(columns);

		db.seekg(0, ios::end);
		const unsigned long READING_LIMIT = (unsigned long)db.tellg() - COORD_DATA_SIZE;
		db.seekg(0, ios::beg);

		unsigned long currentPos = 0;
		unsigned long* coords = new unsigned long[2];

		while (currentPos <= READING_LIMIT)
		{
			db.seekg(currentPos, ios::beg);

			db.read(reinterpret_cast<char*>(coords), COORD_DATA_SIZE);

			Element btreeElement(Key(coords[0], coords[1]), currentPos + COORD_DATA_SIZE);
			
			if (coords[0] < 800)
				int t = 0;

			bTree.Insert(btreeElement);

			currentPos += COORD_DATA_SIZE + MEM_BLOCK_SIZE;
		}
		delete[] coords;
		db.close();
	}
	else
		throw ("Unable to open file " + dbPath);
}

template<typename T>
unsigned long MatrixDatabase<T>::GetPos(Key& coords)
{
	return bTree.Search(coords).second;
}

/******************Methods***************/
template<typename T>
void MatrixDatabase<T>::Initialize(std::string path, unsigned long numOfRows, unsigned long numOfCols, int memBlockSize, bool createSpace)
{
	this->dbPath = path;
	this->MEM_BLOCK_SIZE = memBlockSize;
	this->bTree = BTree(this->BTREE_ORDER);
	if (checkedIfDbExists)
	{
		this->rows += numOfRows;
		this->columns += numOfCols;
	}
	else
	{
		this->rows = numOfRows;
		this->columns = numOfCols;
	}

	//Checking if the database file exists. If not then creating a new blank file
	if(!checkedIfDbExists)
		CheckIfDbExists(dbPath);

	if (createSpace)
	{
		//Initializing the newly added rows and columns to zero in the database
		CreateSpace(rows - numOfRows, numOfCols - columns);
	}

	//Generating the B-Tree
	if ((rows * columns) - (numOfRows * numOfCols) != 0)
		GenerateBTree();
	
}

template <typename T>
std::vector<T> MatrixDatabase<T>::Get(Key* coords, unsigned long count)
{
	db.open(dbPath, ios::out | ios::in | ios::binary);
	if (db.is_open())
	{
		std::vector<T> dataVectorToReturn;
		//Reiterating for every coordinate
		for (unsigned long a = 0; a < count; ++a)
		{
			//Getting the position of the data at the coordinates
			Element pos = bTree.Search(coords[a]); 
			//Positioning the pointer
			db.seekg(pos.second, ios::beg);
			
			char* buffer = new char[MEM_BLOCK_SIZE]; /*A buffer for temporarily
			holding the data*/ 
			db.read(buffer, MEM_BLOCK_SIZE); //Reading the data

			//Reinterpreting the data as pointer to T
			T* reinterpretedData = reinterpret_cast<T*>(buffer);
			for (unsigned long a = 0; a < MEM_BLOCK_SIZE / sizeof(T); ++a)
			{
				dataVectorToReturn.push_back(reinterpretedData[a]);
			}
		}

		db.close();
		return dataVectorToReturn;
	}
	else
		throw ("Unable to open file " + dbPath);
}

template<typename T>
void MatrixDatabase<T>::Edit(T* data, Key* coords, unsigned long count)
{
	db.open(dbPath, ios::in | ios::out | ios::binary);
	if (db.is_open())
	{
		for (unsigned long a = 0; a < count; ++a)
		{
			unsigned long offset = bTree.Search(coords[a]).second;

			db.seekp(offset, ios::beg);

			db.write(reinterpret_cast<const char*>(&data[MEM_BLOCK_SIZE * a]), MEM_BLOCK_SIZE);
		}
		db.close();
	}
	else
		throw "Unable to open database";
}

template<typename T>
void MatrixDatabase<T>::Add(T* data, std::pair<unsigned long, unsigned long>* coords, unsigned long count)
{
	this->db.open(dbPath, ios::in | ios::out | ios::binary);
	if (db.is_open())
	{
		short offset = -1 * (short)sizeof(rows) * 2;
		int dataOffset = MEM_BLOCK_SIZE / sizeof(T); /*The number of elements 
		one block of data has*/
		db.seekp(offset, ios::end);
		
		unsigned long* coord = new unsigned long[2];
		Buffer buffer(5 * (sizeof(rows) + sizeof(columns) + MEM_BLOCK_SIZE));
		int elementOffset = MEM_BLOCK_SIZE + std::abs(offset);
		for (unsigned long a = 0; a < count; ++a)
		{
			coord[0] = coords[a].first;
			coord[1] = coords[a].second;

			//Writing data to buffer
			if (buffer.IsFull())
			{
				buffer.Flush(db);
				buffer.Add(reinterpret_cast<char*>(coord), 2 * sizeof(unsigned long));
				buffer.Add(reinterpret_cast<char*>(&data[a * dataOffset]), MEM_BLOCK_SIZE);
			}
			else
			{
				buffer.Add(reinterpret_cast<char*>(coord), 2 * sizeof(unsigned long));
				buffer.Add(reinterpret_cast<char*>(&data[a * dataOffset]), MEM_BLOCK_SIZE);
			}

			Element e(Key(coord[0], coord[1]), (elementOffset * a) + std::abs(offset));
			bTree.Insert(e);

			//Updating the number of rows and columns in the database
			if (coord[0] > rows)
				rows += coord[0] - rows;
			if (coord[1] > columns)
				columns += coord[1] - columns;
		}
		delete[] coord;

		if (buffer.CurrentSize() != 0)
			buffer.Flush(db);

		//Adding the number of rows and columns
 		db.write(reinterpret_cast<const char*>(&rows), sizeof(rows));
		db.write(reinterpret_cast<const char*>(&columns), sizeof(columns));

		db.close();
	}
	else
		throw ("Unable to open file " + dbPath);
}

template<typename T>
unsigned long MatrixDatabase<T>::Rows() const
{
	return rows;
}

template<typename T>
unsigned long MatrixDatabase<T>::Columns() const
{
	return columns;
}

/******************Operators***************/
template<typename T>
void MatrixDatabase<T>::operator=(const MatrixDatabase<T>& db)
{
	this->dbPath = db.dbPath;
	this->rows = db.rows;
	this->columns = db.columns;
	this->MEM_BLOCK_SIZE = db.MEM_BLOCK_SIZE;
	this->bTree = db.bTree;

	//Checking if the database exists
	CheckIfDbExists(dbPath);
}


