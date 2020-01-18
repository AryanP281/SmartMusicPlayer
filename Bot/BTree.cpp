/**********************Preprocessor Directives*************/
#include "stdafx.h"
#include "BTree.h"

/**********************BTree Class*************/
BTree::BTree() : BTree(0)
{
}

BTree::BTree(int t) : T(t)
{
	//Initializing the root
	this->root = BTreeNode(t);
}

BTree::BTree(int t, BTreeNode root) : T(t)
{
	//Initializing the root
	this->root = root;
}

BTree::BTree(const BTree& tree) : T(tree.T)
{
	*this = tree;
}

BTree::~BTree()
{
}

Element& BTree::Search(const Key& k)
{
	return root.Search(k);
}

void BTree::Traverse() const
{
	std::vector<const Element*> elements; /*A vector for holding the traversed
	elements*/
	
	//Prints the elements in the tree
	root.Print(elements);
}

void BTree::Insert(Element e)
{
	//Checking if the root is full
	if (!root.IsFull())
		root.Insert(e);
	else //The root is full
	{
		BTreeNode newRoot = BTreeNode(T); //The new root node

		//Adding the old root as a child of the new root
		newRoot.children.push_back(this->root);

		//Splitting the old root 
		newRoot.SplitChild(0);

		//Inserting the element
		if (newRoot.keys[0].first.first > e.first.first)
			newRoot.children[0].Insert(e);
		else if (newRoot.keys[0].first.first == e.first.first && newRoot.keys[0].first.second > e.first.second)
			newRoot.children[0].Insert(e);
		else
			newRoot.children[1].Insert(e);

		//Making the new root the root of the tree
		this->root = newRoot;
	}
}

void BTree::operator=(const BTree& tree)
{
	this->T = tree.T;
	this->root = tree.root;
}

/**********************BTreeNode Class*************/
BTreeNode::BTreeNode() : MAX_KEYS(1)
{
}

BTreeNode::BTreeNode(int t) : MAX_KEYS(2 * t - 1)
{
}

BTreeNode::BTreeNode(const BTreeNode& node)
{
	this->operator=(node);
}

bool BTreeNode::IsFull() const
{
	return (keys.size() == MAX_KEYS);
}

Element& BTreeNode::Search(const Key& k)
{
	//Checking if the current node has the key
	for (Element& e : keys)
	{
		if (e.first == k)
			return e;
	}

	if (this->children.size() == 0) //Throwing an error as the node is a leaf and doesn't contain the key 
		throw "Tree doesn't contain the given key";
	else //Looking for element in children nodes
	{
		for (BTreeNode& child : children)
		{
			Element& lastKeyInChild = child.keys[child.keys.size() - 1];
			if (lastKeyInChild.first.first > k.first || (lastKeyInChild.first.first == k.first && lastKeyInChild.first.second >= k.second))
				return child.Search(k);
		}
	}

	//Key wasn't found in any of the children
	throw "Tree doesn't contain the given key";
}

void BTreeNode::Print(std::vector<const Element*>& elements) const
{
	//Printing the nodes keys if it is a leaf
	if (this->children.size() == 0)
	{
		for (const Element& e : keys)
		{
			elements.push_back(&e);
		}
	}
	else
	{
		//Adding the keys of the children nodes to the list
		for (const BTreeNode& child : children)
		{
			child.Print(elements);
		}

		//Adding the current nodes keys
		for (const Element& e : keys)
		{
			elements.push_back(&e);
		}
	}
}

void BTreeNode::SplitChild(unsigned long i)
{
	BTreeNode newNode = BTreeNode((MAX_KEYS + 1) / 2); /*The new node formed after splitting the
	child*/

	//Transferring the last (t - 1) elements of the child to the new node
	std::vector<Element>& _keys = children[i].keys;/*The keys of
	the child node which is to be split*/
	for (unsigned long a = ((_keys.size() - 1) / 2) + 1; a < _keys.size(); ++a)
	{
		newNode.keys.push_back(_keys[a]);
	}

	//Making the last (t - 1) children of the old node the children of the new node
	if (children[i].children.size() != 0)
	{
		for (unsigned long a = ((children[i].children.size() - 1) / 2) + 1; a < children[i].children.size(); ++a)
		{
			newNode.children.push_back(children[i].children.at(a));
		}
	}

	//Moving a key from the child node to this node
	keys.push_back(Element(Key(-1, -1), 0)); //Making room in the keys vector
	for (long a = keys.size() - 2; a > (long)i - 1; --a)
	{
		keys[a + 1] = keys[a];
	}
	keys[i] = _keys[(_keys.size() - 1) / 2];
	_keys.erase(_keys.begin() + ((_keys.size() - 1) / 2), _keys.end()); /*
	Removing the element from the child node*/

	//Making place for the new child
	children.push_back(BTreeNode());
	for (long a = children.size() - 2; a >= i + 1; --a)
	{
		children[a + 1] = children[a];
	}
	children[i + 1] = newNode;
	/*Removing the element from the child node*/
	if (children[i].children.size() != 0)
		children[i].children.erase(children[i].children.begin() + (((children[i].children.size() - 1) / 2) + 1), children[i].children.end());
}

void BTreeNode::Insert(Element e)
{
	if (this->children.size() == 0) //Adding the element to the node directly as it is a leaf
	{
			//Making room in the vector
			keys.push_back(Element(Key(-1, -1), -1));

			//Finding the location of the new key
			for (long a = keys.size() - 2; a >= 0; --a)
			{
				try {
					if (keys[a].first.first > e.first.first)
					{
						keys[a + 1] = keys[a];
					}
					else if (keys[a].first.first == e.first.first && keys[a].first.second > e.first.second)
					{
						keys[a + 1] = keys[a];
					}
					else
					{
						keys[a + 1] = e;
						return;
					}
				}
				catch (std::exception e)
				{
					throw e.what();
				}
			}
			keys[0] = e;
	}
	else
	{
		//Finding the child in which to insert the key
		if (keys[0].first.first > e.first.first || (keys[0].first.first == e.first.first && e.first.second < keys[0].first.second))
		{
			//Checking whether the child is full
			if (children[0].IsFull())
			{
				//Splitting the child node
				SplitChild(0);

				//Determining in which child the new key should be insert
				Element& lastKeyInChild = children[0].keys[children[0].keys.size() - 1];
				if (children[0].keys.size() != 0)
				{
					if (lastKeyInChild.first.first > e.first.first || (lastKeyInChild.first.first == e.first.first && lastKeyInChild.first.second > e.first.second))
						children[0].Insert(e);
					else
						children[1].Insert(e);
				}
				else
					children[0].Insert(e);
			}
			else
				children[0].Insert(e);
		}
		else
		{
			for (long a = keys.size() - 1; a >= 0; --a)
			{
				if (e.first.first > keys[a].first.first || (e.first.first == keys[a].first.first && e.first.second > keys[a].first.second))
				{
					if (children[a + 1].IsFull())
					{
						SplitChild(a + 1);
						//Determining in which child the new key should be inserted
						Element& lastKeyInChild = children[a + 1].keys[children[a + 1].keys.size() - 1];
						if (children[a + 1].keys.size() != 0)
						{
							if (lastKeyInChild.first.first > e.first.first || (e.first.first == lastKeyInChild.first.first && e.first.second < lastKeyInChild.first.second))
								children[a + 1].Insert(e);
							else
								children[a + 2].Insert(e);
						}
						else
							children[a + 1].Insert(e);

					}
					else
						children[a + 1].Insert(e);
					break;
				}
			}
		}
	}
}

void BTreeNode::operator=(const BTreeNode& node)
{
	this->MAX_KEYS = node.MAX_KEYS;
	this->children = node.children;
	this->keys = node.keys;
}
