
/******************Preprocessor Directives***************/
#include "stdafx.h"
#include <sys/stat.h>
#include "Misc.h"

namespace Misc
{
	/******************Functions**************************/
	std::string SubString(const std::string& str, int startPos, char charEncountered)
	{
		std::string subStr = (char*)str[startPos];
		for (; startPos < str.size(); ++startPos)
		{
			if (str[startPos] == charEncountered)
				break;

			subStr += str[startPos];
		}

		return subStr;
	}
}
