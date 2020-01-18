#pragma once
/**********************Preprocessor Directives*************/
#include <vector>
#include <iostream>

/**********************Typedefs*************/
typedef std::pair<unsigned long, unsigned long> Key;
typedef std::pair<Key, unsigned long> Element;

/**********************Classes*************/
class BTreeNode
{
private:
	friend class BTree;
	int MAX_KEYS; //The max number of keys the node can store
	std::vector<Element> keys; //The keys in the node
	std::vector<BTreeNode> children; //The children of the node

public:
	//Constructors And Destructors
	BTreeNode();
	BTreeNode(int t);
	BTreeNode(const BTreeNode& node);

	//Methods
	bool IsFull() const; /*Tells whether the node is full*/
	Element& Search(const Key& k);
	void Print(std::vector<const Element*>& e) const;
	void SplitChild(unsigned long i);
	void Insert(Element e);

	//Operators
	void operator=(const BTreeNode& node);
};

class BTree
{
private:
	int T; //The order of the tree
	BTreeNode root; /*The root node*/

public:
	//Constructors And Destructors
	BTree();
	BTree(int t);
	BTree(int t, BTreeNode root);
	BTree(const BTree& tree);
	~BTree();

	//Methods
	Element& Search(const Key& k);
	void Traverse() const; /*Traverses the tree and prints its keys starting 
	from the leafs*/
	void Insert(Element e); //Adds an element to the tree

	//Operators
	void operator=(const BTree& tree);

};
