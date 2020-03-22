using UnityEngine;
using System;
using System.Collections;

public class LifeSim : MonoBehaviour
{
	private const int DEAD = 0;
	private const int ALIVE = 1;

	private const int Moore = 0;
	private const int VNeumann = 1;

	public readonly int nbrOfCells = 20;
	public float cellLength;
	public bool torusShape;

	private Cell[,,] cellGrid;
	public int[,,] nextStatusGrid;

	private int step = 0;
	public int stepInterval = 20;
	private float timer;
	public float pauseDuration;

	public string ruleKey = "9-20/5-7,12-13,15-16/5/M";
	private string oldRuleKey;
	private int neighbour;
	private int[][] rule;

	public void Start()
	{
		// Init rule array (26x2)
		int[] r = new int[] { DEAD, DEAD };
		this.rule = new int[][] { r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r };

		// Init cellGrid and nextStatusGrid
		this.cellGrid = new Cell[this.nbrOfCells,this.nbrOfCells,this.nbrOfCells];
		this.nextStatusGrid = new int[this.nbrOfCells,this.nbrOfCells,this.nbrOfCells];
		
		for(int i = 0; i < this.nbrOfCells; i++)
        {
			for(int j = 0; j < this.nbrOfCells; j++)
            {
				for(int k = 0; k < this.nbrOfCells; k++)
                {
					// Cell random initial status
					int initCellStatus = UnityEngine.Random.value < 0.8 ? DEAD : ALIVE;
					this.cellGrid[i,j,k] = new Cell(i, j, k, this.cellLength, initCellStatus);
					
					// Next status grid initial value
					this.nextStatusGrid[i,j,k] = initCellStatus;
                }
            }
        }

		this.translateRule();
	}

	public void Update()
	{
		// Update step (quick&dirty way)
		step++;
		if (step % stepInterval == 0)
		{
			// Update cellGrid array
			this.nextStep();
			int midIndex = this.nbrOfCells / 2;
			Debug.Log(this.nextStatusGrid[midIndex, midIndex, midIndex]);
		}
	}

	private void translateRule()
	{ // Transforme la chaine ruleKey en une rule utilisable
	  // Il y a 26*2 cas (de 0 à 26 voisins vivants, selon son propre status) qui doivent être couverts par une règle au pire
	  // (Il y a plus de voisins dans un voisinage de Moore que dans un voisinage de Von Neumann)
	  // Il faut donc 52 bits pour encoder une règle

		//Initialisation du tableau :
		for(int i = 0; i < 26; i++)
        {
			this.rule[i][0] = DEAD;
			this.rule[i][1] = DEAD;
        }

		string[] splittedRule = this.ruleKey.Split('/');
		string[] sustains = splittedRule[0].Split(',');
		string[] births = splittedRule[1].Split(',');
		string[] overpops = splittedRule[2].Split(',');
		string neighbourKind = splittedRule[3];

		string[] aRange;
		int a, b;

		for(int i = 0; i < sustains.Length; i++)
        {
			if (sustains[i].Contains("-"))
            {
				aRange = sustains[i].Split('-');
				a = Int32.Parse(aRange[0]);
				b = Int32.Parse(aRange[1]);
				for(int j = a; j <= b; j++)
                {
					this.rule[j][1] = ALIVE;
                }
            }
            else
            {
				a = Int32.Parse(sustains[i]);
				this.rule[a][1] = ALIVE;
            }
        }

		for(int i = 0; i < births.Length; i++)
		{
			if (births[i].Contains("-"))
			{
				aRange = births[i].Split('-');
				a = Int32.Parse(aRange[0]);
				b = Int32.Parse(aRange[1]);
				for (int j = a; j <= b; j++)
				{
					this.rule[j][0] = ALIVE;
				}
			}
			else
			{
				a = Int32.Parse(sustains[i]);
				this.rule[a][0] = ALIVE;
			}
		}

		for(int i = 0; i < overpops.Length; i++)
        {
			if (overpops[i].Contains("-"))
            {
				aRange = overpops[i].Split('-');
				a = Int32.Parse(aRange[0]);
				b = Int32.Parse(aRange[1]);
				for (int j = 0; j <= b; j++)
                {
					this.rule[j][1] = DEAD;
                }
            }
            else
            {
				a = Int32.Parse(overpops[i]);
				this.rule[a][1] = DEAD;
            }
        }
		
		if (neighbourKind.Equals("M"))
        {
			this.neighbour = Moore;
        }
        else if (neighbourKind.Equals("VN"))
        {
			this.neighbour = VNeumann;
        }
        else
        {
			throw new System.ArgumentException(neighbourKind + " est inconnu");
        }
		return;
	}

	private int countMooreNeighbours(int x, int y, int z)
	{ //Compte le nombre de voisins vivants selon le voisiange de Moore (un cube)
		int count = 0;
		for (int i = -1; i <= 1; i++)
		{
			for (int j = -1; j <= 1; j++)
			{
				for (int k = -1; k <= 1; k++)
				{
					if (this.torusShape)
					{
						if (!(i == 0 && j == 0 && k == 0))
						{ //Plusieurs opérations modulo pour assurer un résultat positif
							count += this.cellGrid[((x + i) % this.nbrOfCells + this.nbrOfCells) % nbrOfCells,((y + j) % this.nbrOfCells + this.nbrOfCells) % nbrOfCells,((z + k) % this.nbrOfCells + this.nbrOfCells) % nbrOfCells].getStatus();
						}
					}
					else
					{
						if ((x + i >= 0 && x + i < this.nbrOfCells) && (y + j >= 0 && y + j < this.nbrOfCells) && (z + k >= 0 && z + k < this.nbrOfCells)
							&& !(i == 0 && j == 0 && k == 0))
						{
							count += this.cellGrid[x + i,y + j,z + k].getStatus();
						}
					}

				}
			}
		}
		return count;
	}

	private int countVonNeumannNeighbours(int x, int y, int z)
	{ //Compte le nombre de voisins vivants selon le voisinage de Von Neumann (une croix)
		int count = 0;
		count += this.cellGrid[(x + 1) % this.nbrOfCells,y,z].getStatus();
		if (x == 0)
		{
			if (this.torusShape)
			{
				count += this.cellGrid[this.nbrOfCells - 1,y,z].getStatus();
			}
		}

		count += this.cellGrid[x,(y + 1) % this.nbrOfCells,z].getStatus();
		if (y == 0)
		{
			if (this.torusShape)
			{
				count += this.cellGrid[x,this.nbrOfCells - 1,z].getStatus();
			}
		}

		count += this.cellGrid[x,y,(z + 1) % this.nbrOfCells].getStatus();
		if (z == 0)
		{
			if (this.torusShape)
			{
				count += this.cellGrid[x,y,this.nbrOfCells - 1].getStatus();
			}
		}
		return count;
	}

	private void buildNextGrid()
	{
		for (int x = 0; x < this.nbrOfCells; x++)
		{
			for (int y = 0; y < this.nbrOfCells; y++)
			{
				for (int z = 0; z < this.nbrOfCells; z++)
				{
					if (this.neighbour == Moore)
                    {
						int a = this.cellGrid[x,y,z].getStatus();
						int b = this.countMooreNeighbours(x, y, z);
						Debug.Log("A value (should 0 or 1) -> " + a);
						Debug.Log("B value (should 0 < 25) -> " + b);
						int c = this.rule[b][a];
						this.nextStatusGrid[x,y,z] = c;
					}
					else if (this.neighbour == VNeumann)
                    {
						this.nextStatusGrid[x,y,z] = this.rule[this.countVonNeumannNeighbours(x, y, z)][this.cellGrid[x,y,z].getStatus()];
					}
				}
			}
		}
	}

	private void nextStep()
	{
		this.buildNextGrid();
		for (int x = 0; x < this.nbrOfCells; x++)
		{
			for (int y = 0; y < this.nbrOfCells; y++)
			{
				for (int z = 0; z < this.nbrOfCells; z++)
				{
					this.cellGrid[x,y,z].changeStatus(this.nextStatusGrid[x,y,z]);
				}
			}
		}
	}

}
